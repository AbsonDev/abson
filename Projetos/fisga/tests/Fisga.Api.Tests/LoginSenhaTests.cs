using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fisga.Api.Contracts;
using Fisga.Application.Identidade;
using Fisga.Domain.Identidade;
using Fisga.Infrastructure.Identity;
using Fisga.Infrastructure.Persistence;

namespace Fisga.Api.Tests;

[Collection("api")]
public sealed class LoginSenhaTests : IAsyncLifetime
{
    private const string Senha = "senha-segura-123";

    private readonly ApiTestFixture _fixture;
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;
    private readonly FisgaDbContext _db;
    private readonly ICadastroDeUsuario _cadastro;
    private readonly UserManager<ApplicationUser> _usuarios;

    public LoginSenhaTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
        _scope = fixture.Factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<FisgaDbContext>();
        _cadastro = _scope.ServiceProvider.GetRequiredService<ICadastroDeUsuario>();
        _usuarios = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    }

    public async Task InitializeAsync()
    {
        await _db.Usuarios.ExecuteDeleteAsync();
        await _db.Users.ExecuteDeleteAsync();
        EmailSenderFake.Limpar();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _scope.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Usuario> CriarUsuarioAsync(string email, bool confirmado)
    {
        var usuario = await _cadastro.CriarComSenhaAsync(email, "Pescador", Senha);

        if (confirmado)
        {
            var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
            conta!.EmailConfirmed = true;
            await _usuarios.UpdateAsync(conta);
        }

        return usuario;
    }

    [Fact]
    public async Task Entrar_com_credenciais_validas_emite_cookie_fisga_auth()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        await CriarUsuarioAsync(email, confirmado: true);

        using var clienteSemCookies = _fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = false });

        var resposta = await clienteSemCookies.PostAsJsonAsync(
            "/api/auth/entrar", new { email, senha = Senha });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.Content.ReadFromJsonAsync<EntrarResponse>();
        Assert.NotNull(corpo);
        Assert.True(corpo!.Autenticado);

        Assert.True(
            resposta.Headers.TryGetValues("Set-Cookie", out var cookies),
            "O login válido deve emitir o cookie de autenticação.");
        Assert.Contains(
            cookies!,
            cookie => cookie.StartsWith("fisga_auth=", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            cookies!,
            cookie => cookie.StartsWith("fisga_sessao=;", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Entrar_com_email_nao_confirmado_nao_autentica()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var usuario = await CriarUsuarioAsync(email, confirmado: false);

        using var clienteSemCookies = _fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = false });

        var resposta = await clienteSemCookies.PostAsJsonAsync(
            "/api/auth/entrar", new { email, senha = Senha });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.Content.ReadFromJsonAsync<EntrarResponse>();
        Assert.NotNull(corpo);
        Assert.False(corpo!.Autenticado);

        Assert.False(
            resposta.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies!.Any(c => c.StartsWith("fisga_auth=", StringComparison.OrdinalIgnoreCase)),
            "E-mail não confirmado não deve emitir cookie de autenticação.");

        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.False(conta!.EmailConfirmed);
    }

    [Fact]
    public async Task Entrar_com_email_nao_confirmado_reenvia_link_de_confirmacao()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        await CriarUsuarioAsync(email, confirmado: false);

        var resposta = await _client.PostAsJsonAsync(
            "/api/auth/entrar", new { email, senha = Senha });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.Content.ReadFromJsonAsync<EntrarResponse>();
        Assert.False(corpo!.Autenticado);

        Assert.Contains(
            EmailSenderFake.Enviados,
            e => e.Destino == email && e.Assunto == "Confirme seu e-mail no Fisga");
    }

    [Fact]
    public async Task Entrar_com_email_nao_confirmado_e_senha_errada_nao_envia_email()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        await CriarUsuarioAsync(email, confirmado: false);

        var resposta = await _client.PostAsJsonAsync(
            "/api/auth/entrar", new { email, senha = "senha-errada-999" });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.Content.ReadFromJsonAsync<EntrarResponse>();
        Assert.False(corpo!.Autenticado);
        Assert.Equal("E-mail ou senha incorretos.", corpo.Mensagem);

        Assert.DoesNotContain(
            EmailSenderFake.Enviados,
            e => e.Destino == email && e.Assunto == "Confirme seu e-mail no Fisga");
    }

    [Fact]
    public async Task Entrar_com_senha_errada_incrementa_lockout()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var usuario = await CriarUsuarioAsync(email, confirmado: true);

        for (var i = 0; i < 5; i++)
        {
            var resposta = await _client.PostAsJsonAsync(
                "/api/auth/entrar", new { email, senha = "senha-errada-999" });

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await resposta.Content.ReadFromJsonAsync<EntrarResponse>();
            Assert.False(corpo!.Autenticado);
        }

        var contaFresca = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == usuario.Id);
        Assert.NotNull(contaFresca.LockoutEnd);
        Assert.True(
            contaFresca.LockoutEnd > DateTimeOffset.UtcNow,
            "A conta deve ficar bloqueada após 5 tentativas.");

        // Mesmo com a senha correta, a conta bloqueada não pode entrar.
        var tentativaCorreta = await _client.PostAsJsonAsync(
            "/api/auth/entrar", new { email, senha = Senha });
        var corpoCorreto = await tentativaCorreta.Content.ReadFromJsonAsync<EntrarResponse>();
        Assert.False(corpoCorreto!.Autenticado);
    }

    [Fact]
    public async Task Entrar_com_email_nao_confirmado_e_senha_errada_tambem_conta_lockout()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var usuario = await CriarUsuarioAsync(email, confirmado: false);

        for (var i = 0; i < 5; i++)
        {
            var resposta = await _client.PostAsJsonAsync(
                "/api/auth/entrar", new { email, senha = "senha-errada-999" });

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            Assert.False((await resposta.Content.ReadFromJsonAsync<EntrarResponse>())!.Autenticado);
        }

        var conta = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == usuario.Id);
        Assert.NotNull(conta.LockoutEnd);
        Assert.True(conta.LockoutEnd > DateTimeOffset.UtcNow);

        var bloqueada = await _client.PostAsJsonAsync(
            "/api/auth/entrar", new { email, senha = "senha-errada-999" });
        var corpo = await bloqueada.Content.ReadFromJsonAsync<EntrarResponse>();
        Assert.False(corpo!.Autenticado);
        Assert.Contains("Muitas tentativas", corpo.Mensagem);
    }

    [Fact]
    public async Task Entrar_recusa_senha_acima_de_128_antes_da_identity()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var usuario = await CriarUsuarioAsync(email, confirmado: true);

        var resposta = await _client.PostAsJsonAsync(
            "/api/auth/entrar", new { email, senha = new string('a', 129) });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.False((await resposta.Content.ReadFromJsonAsync<EntrarResponse>())!.Autenticado);

        var conta = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == usuario.Id);
        Assert.Equal(0, conta.AccessFailedCount);
    }

    [Fact]
    public async Task Entrar_recusa_email_acima_de_254_antes_da_identity()
    {
        var email = $"{new string('a', 243)}@exemplo.com";

        var resposta = await _client.PostAsJsonAsync(
            "/api/auth/entrar", new { email, senha = Senha });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.False((await resposta.Content.ReadFromJsonAsync<EntrarResponse>())!.Autenticado);
    }

    [Fact]
    public async Task Entrar_com_email_inexistente_retorna_mesma_mensagem_generica()
    {
        var respostaInexistente = await _client.PostAsJsonAsync(
            "/api/auth/entrar", new { email = $"inexistente-{Guid.NewGuid():N}@exemplo.com", senha = Senha });

        var email = $"{Guid.NewGuid():N}@exemplo.com";
        await CriarUsuarioAsync(email, confirmado: true);
        var respostaSenhaErrada = await _client.PostAsJsonAsync(
            "/api/auth/entrar", new { email, senha = "senha-errada-999" });

        var corpoInexistente = await respostaInexistente.Content.ReadFromJsonAsync<EntrarResponse>();
        var corpoSenhaErrada = await respostaSenhaErrada.Content.ReadFromJsonAsync<EntrarResponse>();

        Assert.Equal(HttpStatusCode.OK, respostaInexistente.StatusCode);
        Assert.False(corpoInexistente!.Autenticado);
        Assert.False(corpoSenhaErrada!.Autenticado);
        Assert.Equal(corpoInexistente.Mensagem, corpoSenhaErrada.Mensagem);
    }
}

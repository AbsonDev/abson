using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fisga.Api.Contracts;
using Fisga.Application.Identidade;
using Fisga.Domain.Identidade;
using Fisga.Infrastructure.Identity;
using Fisga.Infrastructure.Persistence;

namespace Fisga.Api.Tests;

[Collection("api")]
public sealed class ConfirmacaoResetTests : IAsyncLifetime
{
    private const string Senha = "senha-segura-123";
    private const string SenhaNova = "nova-senha-segura-456";

    private readonly HttpClient _client;
    private readonly IServiceScope _scope;
    private readonly FisgaDbContext _db;
    private readonly ICadastroDeUsuario _cadastro;
    private readonly UserManager<ApplicationUser> _usuarios;

    public ConfirmacaoResetTests(ApiTestFixture fixture)
    {
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
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _scope.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Confirmar_email_valido_marca_ambos_os_estados()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var usuario = await _cadastro.CriarComSenhaAsync(email, "Pescador", Senha);
        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        var token = await _usuarios.GenerateEmailConfirmationTokenAsync(conta!);

        var resposta = await _client.PostAsJsonAsync(
            "/api/auth/confirmar-email", new { uid = usuario.Id.ToString(), token });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var contaAtualizada = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == usuario.Id);
        Assert.True(contaAtualizada.EmailConfirmed);

        var usuarioAtualizado = await _db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == usuario.Id);
        Assert.NotNull(usuarioAtualizado.EmailVerificadoEm);
    }

    [Fact]
    public async Task Confirmar_email_com_token_invalido_nao_marca_nada()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var usuario = await _cadastro.CriarComSenhaAsync(email, "Pescador", Senha);

        var resposta = await _client.PostAsJsonAsync(
            "/api/auth/confirmar-email", new { uid = usuario.Id.ToString(), token = "token-invalido" });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);

        var conta = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == usuario.Id);
        Assert.False(conta.EmailConfirmed);

        var usuarioAtualizado = await _db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == usuario.Id);
        Assert.Null(usuarioAtualizado.EmailVerificadoEm);
    }

    [Fact]
    public async Task Esqueci_senha_retorna_202_generico_para_email_existente_e_inexistente()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        await _cadastro.CriarComSenhaAsync(email, "Pescador", Senha);

        var existente = await _client.PostAsJsonAsync(
            "/api/auth/esqueci-senha", new { email });
        var inexistente = await _client.PostAsJsonAsync(
            "/api/auth/esqueci-senha", new { email = $"nao-existe-{Guid.NewGuid():N}@exemplo.com" });

        Assert.Equal(HttpStatusCode.Accepted, existente.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, inexistente.StatusCode);

        var corpoExistente = await existente.Content.ReadFromJsonAsync<EsqueciSenhaResponse>();
        var corpoInexistente = await inexistente.Content.ReadFromJsonAsync<EsqueciSenhaResponse>();

        Assert.Equal(corpoExistente!.Mensagem, corpoInexistente!.Mensagem);
    }

    [Fact]
    public async Task Redefinir_senha_valida_troca_senha_e_invalida_token()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var usuario = await _cadastro.CriarComSenhaAsync(email, "Pescador", Senha);
        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        var securityStampOriginal = conta!.SecurityStamp;
        var token = await _usuarios.GeneratePasswordResetTokenAsync(conta);

        var resposta = await _client.PostAsJsonAsync(
            "/api/auth/redefinir-senha",
            new { uid = usuario.Id.ToString(), token, senha = SenhaNova });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var contaAtualizada = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == usuario.Id);
        Assert.True(await _usuarios.CheckPasswordAsync(contaAtualizada, SenhaNova));
        Assert.False(await _usuarios.CheckPasswordAsync(contaAtualizada, Senha));
        Assert.NotEqual(securityStampOriginal, contaAtualizada.SecurityStamp);
    }

    [Fact]
    public async Task Redefinir_senha_token_reutilizado_falha()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var usuario = await _cadastro.CriarComSenhaAsync(email, "Pescador", Senha);
        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        var token = await _usuarios.GeneratePasswordResetTokenAsync(conta!);

        var primeira = await _client.PostAsJsonAsync(
            "/api/auth/redefinir-senha",
            new { uid = usuario.Id.ToString(), token, senha = SenhaNova });
        Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);

        var reuso = await _client.PostAsJsonAsync(
            "/api/auth/redefinir-senha",
            new { uid = usuario.Id.ToString(), token, senha = "outra-senha-789" });

        Assert.Equal(HttpStatusCode.BadRequest, reuso.StatusCode);
    }
}

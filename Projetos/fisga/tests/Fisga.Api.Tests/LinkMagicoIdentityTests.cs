using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fisga.Api.Contracts;
using Fisga.Application.Identidade;
using Fisga.Application.Seguranca;
using Fisga.Domain.Identidade;
using Fisga.Infrastructure.Persistence;

namespace Fisga.Api.Tests;

/// <summary>
/// Cobertura ponta a ponta da migração do link mágico para a ASP.NET Core Identity:
/// o cookie <c>fisga_auth</c> é a única autenticação e a sessão de dispositivo é
/// criada pelo endpoint via IdentitySessionService.AssinarAsync — não pelo serviço
/// de token, nem pelo middleware legado <c>fisga_sessao</c>.
/// </summary>
[Collection("api")]
public sealed class LinkMagicoIdentityTests : IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;
    private readonly IServiceScope _scope;
    private readonly FisgaDbContext _db;

    public LinkMagicoIdentityTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _scope = fixture.Factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<FisgaDbContext>();
    }

    public async Task InitializeAsync()
    {
        // Ordem explícita: sessões e tokens apontam para usuários.
        await _db.Sessoes.ExecuteDeleteAsync();
        await _db.TokensDeLogin.ExecuteDeleteAsync();
        await _db.Usuarios.ExecuteDeleteAsync();
        await _db.Users.ExecuteDeleteAsync();
    }

    public Task DisposeAsync()
    {
        _scope.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Usuario_novo_confirma_link_e_entra_com_cookie_da_identity()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var link = await SolicitarLinkAsync(email);

        using var cliente = CriarClienteSemRedirect();
        var resposta = await ConfirmarAsync(cliente, link);

        Assert.Equal(HttpStatusCode.Found, resposta.StatusCode);
        Assert.Contains("/boas-vindas?novo=1", resposta.Headers.Location!.ToString());
        Assert.True(TemCookieDeAutenticacao(resposta), "A confirmação deve emitir o cookie fisga_auth.");
        Assert.False(TemCookieLegado(resposta), "A confirmação não deve gravar o cookie fisga_sessao.");

        var me = await cliente.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.NotNull(me);
        Assert.True(me!.Autenticado);
        Assert.Equal(email, me.Usuario!.Email);

        var usuario = await _db.Usuarios.SingleAsync(u => u.Email == Email.Criar(email));
        var conta = await _db.Users.SingleAsync(u => u.Email == email);
        Assert.Equal(usuario.Id, conta.Id);
        Assert.True(conta.EmailConfirmed);
        Assert.Null(conta.PasswordHash);

        // A sessão de dispositivo é criada pelo endpoint (AssinarAsync), com o
        // claim sessao_id apontando para a linha — nunca pelo serviço de token.
        var sessoes = await _db.Sessoes.Where(s => s.UsuarioId == usuario.Id).ToListAsync();
        var sessao = Assert.Single(sessoes);
        Assert.Equal(TokensSeguros.Hash(sessao.Id.ToString()), sessao.TokenHash);
        Assert.True(sessao.EstaAtiva(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Usuario_existente_confirma_link_sem_duplicar_a_conta()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var cadastro = _scope.ServiceProvider.GetRequiredService<ICadastroDeUsuario>();
        var existente = await cadastro.GarantirUsuarioAsync(
            Email.Criar(email), "Pescador", emailVerificado: true);

        var link = await SolicitarLinkAsync(email);
        using var cliente = CriarClienteSemRedirect();
        var resposta = await ConfirmarAsync(cliente, link);

        Assert.Equal(HttpStatusCode.Found, resposta.StatusCode);
        Assert.True(TemCookieDeAutenticacao(resposta));

        var me = await cliente.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.True(me!.Autenticado);
        Assert.Equal(existente.Id, me.Usuario!.Id);

        Assert.Equal(1, await _db.Usuarios.CountAsync(u => u.Email == Email.Criar(email)));
        Assert.Equal(1, await _db.Users.CountAsync(u => u.Email == email));
    }

    [Fact]
    public async Task Link_magico_confirma_conta_de_senha_e_desbloqueia_login_por_senha()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        const string senha = "senha-segura-123";
        var cadastro = _scope.ServiceProvider.GetRequiredService<ICadastroDeUsuario>();
        var usuario = await cadastro.CriarComSenhaAsync(email, "Pescador", senha);

        var contaAntes = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == usuario.Id);
        Assert.False(contaAntes.EmailConfirmed);

        var link = await SolicitarLinkAsync(email);
        using var cliente = CriarClienteSemRedirect();
        var resposta = await ConfirmarAsync(cliente, link);

        Assert.Equal(HttpStatusCode.Found, resposta.StatusCode);
        Assert.True(TemCookieDeAutenticacao(resposta));

        var usuarioDepois = await _db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == usuario.Id);
        Assert.NotNull(usuarioDepois.EmailVerificadoEm);
        var contaDepois = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == usuario.Id);
        Assert.True(contaDepois.EmailConfirmed);
        Assert.NotNull(contaDepois.PasswordHash);

        // RequireConfirmedEmail = true: sem a confirmação acima este login ficaria bloqueado.
        using var clienteSenha = _fixture.Factory.CreateClient();
        var login = await clienteSenha.PostAsJsonAsync("/api/auth/entrar", new { email, senha });
        var corpo = await login.Content.ReadFromJsonAsync<EntrarResponse>();
        Assert.NotNull(corpo);
        Assert.True(corpo!.Autenticado);
    }

    [Fact]
    public async Task Segundo_acesso_redireciona_para_estado_ok()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";

        var primeiroLink = await SolicitarLinkAsync(email);
        using var clienteNovo = CriarClienteSemRedirect();
        var primeira = await ConfirmarAsync(clienteNovo, primeiroLink);
        Assert.Contains("/boas-vindas?novo=1", primeira.Headers.Location!.ToString());

        // Envelhece o token usado para liberar a guarda de reenvio (60 s).
        await _db.TokensDeLogin
            .Where(t => t.Email == email)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.CriadoEm, DateTimeOffset.UtcNow.AddMinutes(-2)));

        var segundoLink = await SolicitarLinkAsync(email);
        using var clienteExistente = CriarClienteSemRedirect();
        var segunda = await ConfirmarAsync(clienteExistente, segundoLink);

        Assert.Contains("/entrar/confirmar?estado=ok", segunda.Headers.Location!.ToString());
        Assert.Equal(1, await _db.Usuarios.CountAsync(u => u.Email == Email.Criar(email)));
    }

    [Fact]
    public async Task Link_reutilizado_falha_e_nao_emite_cookie()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var link = await SolicitarLinkAsync(email);

        using var primeiroCliente = CriarClienteSemRedirect();
        var primeira = await ConfirmarAsync(primeiroCliente, link);
        Assert.Equal(HttpStatusCode.Found, primeira.StatusCode);

        using var clienteReuso = CriarClienteSemRedirect();
        var segunda = await ConfirmarAsync(clienteReuso, link);

        Assert.Contains("estado=link_ja_usado", segunda.Headers.Location!.ToString());
        Assert.False(TemCookieDeAutenticacao(segunda));
    }

    [Fact]
    public async Task Link_expirado_falha_e_nao_cria_nada()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var link = await SolicitarLinkAsync(email);

        await _db.TokensDeLogin
            .Where(t => t.Email == email)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiraEm, DateTimeOffset.UtcNow.AddMinutes(-1)));

        using var cliente = CriarClienteSemRedirect();
        var resposta = await ConfirmarAsync(cliente, link);

        Assert.Contains("estado=link_expirado", resposta.Headers.Location!.ToString());
        Assert.False(TemCookieDeAutenticacao(resposta));
        Assert.Equal(0, await _db.Usuarios.CountAsync(u => u.Email == Email.Criar(email)));
        Assert.Equal(0, await _db.Users.CountAsync(u => u.Email == email));
    }

    [Fact]
    public async Task Cookie_legado_fisga_sessao_nao_autentica_mais()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var cadastro = _scope.ServiceProvider.GetRequiredService<ICadastroDeUsuario>();
        var usuario = await cadastro.GarantirUsuarioAsync(
            Email.Criar(email), null, emailVerificado: true);

        var tokenLegado = "token-legado-" + Guid.NewGuid().ToString("N");
        var sessaoLegada = Sessao.Criar(
            usuario.Id,
            TokensSeguros.Hash(tokenLegado),
            "Navegador antigo",
            null,
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(30));
        _db.Sessoes.Add(sessaoLegada);
        await _db.SaveChangesAsync();

        using var cliente = _fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = false });
        cliente.DefaultRequestHeaders.Add("Cookie", $"fisga_sessao={tokenLegado}");

        var me = await cliente.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.NotNull(me);
        Assert.False(me!.Autenticado, "O cookie fisga_sessao não deve mais autenticar.");

        var protegido = await cliente.GetAsync("/api/conta/sessoes");
        Assert.Equal(HttpStatusCode.Unauthorized, protegido.StatusCode);
    }

    private async Task<string> SolicitarLinkAsync(string email)
    {
        using var cliente = _fixture.Factory.CreateClient();
        var resposta = await cliente.PostAsJsonAsync("/api/auth/solicitar", new { email });
        Assert.Equal(HttpStatusCode.Accepted, resposta.StatusCode);

        var corpo = await resposta.Content.ReadFromJsonAsync<SolicitarLinkResponse>();
        Assert.NotNull(corpo);
        Assert.False(string.IsNullOrWhiteSpace(corpo!.LinkDesenvolvimento),
            "Em Development o link deve ser devolvido para o teste.");
        return corpo.LinkDesenvolvimento!;
    }

    private HttpClient CriarClienteSemRedirect() =>
        _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static Task<HttpResponseMessage> ConfirmarAsync(HttpClient cliente, string link)
    {
        var uri = new Uri(link, UriKind.Absolute);
        return cliente.GetAsync(uri.PathAndQuery);
    }

    private static bool TemCookieDeAutenticacao(HttpResponseMessage resposta) =>
        resposta.Headers.TryGetValues("Set-Cookie", out var cookies)
        && cookies.Any(c => c.StartsWith("fisga_auth=", StringComparison.OrdinalIgnoreCase));

    private static bool TemCookieLegado(HttpResponseMessage resposta) =>
        resposta.Headers.TryGetValues("Set-Cookie", out var cookies)
        && cookies.Any(c => c.StartsWith("fisga_sessao=", StringComparison.OrdinalIgnoreCase));
}

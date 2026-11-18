using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fisga.Api.Contracts;
using Fisga.Application.Identidade;
using Fisga.Domain.Common;
using Fisga.Domain.Identidade;
using Fisga.Infrastructure.Identity;
using Fisga.Infrastructure.Persistence;

namespace Fisga.Api.Tests;

/// <summary>
/// Conectar Google em /conta SEM credenciais reais: o serviço é exercitado com
/// <see cref="GoogleUsuario"/> montado diretamente e os endpoints são testados
/// nos caminhos que não dependem do handshake OAuth (503, consentimento,
/// intenção e cookie). Nunca assinamos um cookie novo nem criamos sessão.
/// </summary>
[Collection("api")]
public sealed class ConectarGoogleTests : IAsyncLifetime
{
    private const string Senha = "senha-segura-123";

    private readonly ApiTestFixture _fixture;
    private readonly IServiceScope _scope;
    private readonly FisgaDbContext _db;
    private readonly ICadastroDeUsuario _cadastro;
    private readonly UserManager<ApplicationUser> _usuarios;
    private readonly ConsentimentoService _consentimento;
    private readonly LegalOptions _opcoesLegais;
    private readonly ConexaoGoogleService _conexao;

    public ConectarGoogleTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _scope = fixture.Factory.Services.CreateScope();
        var sp = _scope.ServiceProvider;
        _db = sp.GetRequiredService<FisgaDbContext>();
        _cadastro = sp.GetRequiredService<ICadastroDeUsuario>();
        _usuarios = sp.GetRequiredService<UserManager<ApplicationUser>>();
        _consentimento = sp.GetRequiredService<ConsentimentoService>();
        _opcoesLegais = sp.GetRequiredService<LegalOptions>();
        _conexao = sp.GetRequiredService<ConexaoGoogleService>();
    }

    public async Task InitializeAsync()
    {
        // Ordem explícita: logins e sessões apontam para usuários.
        await _db.UserLogins.ExecuteDeleteAsync();
        await _db.Sessoes.ExecuteDeleteAsync();
        await _db.TokensDeLogin.ExecuteDeleteAsync();
        await _db.Consentimentos.ExecuteDeleteAsync();
        await _db.Usuarios.ExecuteDeleteAsync();
        await _db.Users.ExecuteDeleteAsync();
    }

    public Task DisposeAsync()
    {
        _scope.Dispose();
        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------------
    // Regras de vinculação do serviço
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Mesmo_email_vincula_e_mantem_o_mesmo_usuario_sem_criar_sessao()
    {
        var conta = await CriarContaConfirmadaAsync();
        var subject = NovoSubject();
        var sessoesAntes = await _db.Sessoes.CountAsync();

        await _conexao.ConectarAsync(
            conta, NovoGoogle(subject, conta.Email!, verified: true));

        var vinculado = await _usuarios.FindByLoginAsync(GoogleIdentityValidator.Provider, subject);
        Assert.NotNull(vinculado);
        Assert.Equal(conta.Id, vinculado!.Id);
        Assert.Equal(1, await _db.UserLogins.CountAsync(l =>
            l.LoginProvider == GoogleIdentityValidator.Provider && l.ProviderKey == subject));
        Assert.Equal(sessoesAntes, await _db.Sessoes.CountAsync());
        Assert.Same(conta, vinculado);
    }

    [Fact]
    public async Task Email_diferente_recusa_sem_gravar_nada()
    {
        var conta = await CriarContaConfirmadaAsync();
        var subject = NovoSubject();
        var sessoesAntes = await _db.Sessoes.CountAsync();

        var excecao = await Assert.ThrowsAsync<DomainException>(() => _conexao.ConectarAsync(
            conta, NovoGoogle(subject, "outro@exemplo.com", verified: true)));

        Assert.Equal("google_email_divergente", excecao.Codigo);
        Assert.Null(await _usuarios.FindByLoginAsync(GoogleIdentityValidator.Provider, subject));
        Assert.Equal(0, await _db.UserLogins.CountAsync());
        Assert.Equal(sessoesAntes, await _db.Sessoes.CountAsync());
    }

    [Fact]
    public async Task Conta_local_nao_confirmada_recusa_sem_vincular()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var usuario = await _cadastro.CriarComSenhaAsync(email, "Pescador", Senha);
        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.NotNull(conta);
        Assert.False(conta!.EmailConfirmed);
        var subject = NovoSubject();

        var excecao = await Assert.ThrowsAsync<DomainException>(() => _conexao.ConectarAsync(
            conta, NovoGoogle(subject, email, verified: true)));

        Assert.Equal("google_email_nao_verificado_localmente", excecao.Codigo);
        Assert.Null(await _usuarios.FindByLoginAsync(GoogleIdentityValidator.Provider, subject));
        Assert.Equal(0, await _db.UserLogins.CountAsync());
    }

    [Fact]
    public async Task Google_sem_email_verificado_recusa_sem_vincular()
    {
        var conta = await CriarContaConfirmadaAsync();
        var subject = NovoSubject();

        var excecao = await Assert.ThrowsAsync<DomainException>(() => _conexao.ConectarAsync(
            conta, NovoGoogle(subject, conta.Email!, verified: false)));

        Assert.Equal("google_email_nao_verificado", excecao.Codigo);
        Assert.Null(await _usuarios.FindByLoginAsync(GoogleIdentityValidator.Provider, subject));
        Assert.Equal(0, await _db.UserLogins.CountAsync());
    }

    [Fact]
    public async Task Subject_ja_vinculado_a_outro_usuario_recusa_sem_trocar_de_conta()
    {
        var conta = await CriarContaConfirmadaAsync();
        var outro = await CriarContaConfirmadaAsync();
        Assert.NotEqual(conta.Id, outro.Id);
        var subject = NovoSubject();
        Assert.True((await _usuarios.AddLoginAsync(outro, Login(subject))).Succeeded);

        var excecao = await Assert.ThrowsAsync<DomainException>(() => _conexao.ConectarAsync(
            conta, NovoGoogle(subject, conta.Email!, verified: true)));

        Assert.Equal("google_vinculo_falhou", excecao.Codigo);
        var dono = await _usuarios.FindByLoginAsync(GoogleIdentityValidator.Provider, subject);
        Assert.NotNull(dono);
        Assert.Equal(outro.Id, dono!.Id);
        Assert.Empty(await _usuarios.GetLoginsAsync(conta));
        Assert.Equal(1, await _db.UserLogins.CountAsync(l =>
            l.LoginProvider == GoogleIdentityValidator.Provider && l.ProviderKey == subject));
    }

    [Fact]
    public async Task Ja_vinculado_ao_usuario_atual_confirma_sem_duplicar_linha()
    {
        var conta = await CriarContaConfirmadaAsync();
        var subject = NovoSubject();
        Assert.True((await _usuarios.AddLoginAsync(conta, Login(subject))).Succeeded);

        await _conexao.ConectarAsync(conta, NovoGoogle(subject, conta.Email!, verified: true));

        var dono = await _usuarios.FindByLoginAsync(GoogleIdentityValidator.Provider, subject);
        Assert.NotNull(dono);
        Assert.Equal(conta.Id, dono!.Id);
        Assert.Equal(1, await _db.UserLogins.CountAsync(l =>
            l.LoginProvider == GoogleIdentityValidator.Provider && l.ProviderKey == subject));
    }

    // ---------------------------------------------------------------------
    // Intenção protegida
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Intencao_valida_para_o_mesmo_usuario()
    {
        var usuarioId = Guid.NewGuid();
        var valor = await _conexao.CriarIntencaoAsync(usuarioId);

        Assert.False(string.IsNullOrWhiteSpace(valor));
        Assert.DoesNotContain(usuarioId.ToString(), valor, StringComparison.Ordinal);
        await _conexao.ValidarIntencaoAsync(valor, usuarioId);
    }

    [Fact]
    public async Task Intencao_de_outro_usuario_e_recusada()
    {
        var valor = await _conexao.CriarIntencaoAsync(Guid.NewGuid());

        var excecao = await Assert.ThrowsAsync<DomainException>(() =>
            _conexao.ValidarIntencaoAsync(valor, Guid.NewGuid()));

        Assert.Equal("google_intencao_invalida", excecao.Codigo);
    }

    [Fact]
    public async Task Intencao_tamperada_e_recusada()
    {
        var usuarioId = Guid.NewGuid();
        var valor = await _conexao.CriarIntencaoAsync(usuarioId);
        var tamperado = valor.Substring(0, Math.Max(1, valor.Length - 2)) + "xx";

        var excecao = await Assert.ThrowsAsync<DomainException>(() =>
            _conexao.ValidarIntencaoAsync(tamperado, usuarioId));

        Assert.Equal("google_intencao_invalida", excecao.Codigo);
    }

    [Fact]
    public async Task Intencao_expirada_e_recusada()
    {
        var usuarioId = Guid.NewGuid();
        var protetor = _scope.ServiceProvider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(ConexaoGoogleService.PropositoIntencao);
        var passado = DateTimeOffset.UtcNow.AddMinutes(-10).UtcTicks;
        var valor = protetor.Protect($"{usuarioId:N}|{Guid.NewGuid():N}|{passado}");

        var excecao = await Assert.ThrowsAsync<DomainException>(() =>
            _conexao.ValidarIntencaoAsync(valor, usuarioId));

        Assert.Equal("google_intencao_expirada", excecao.Codigo);
    }

    [Fact]
    public async Task Validador_propaga_email_verificado_para_a_conexao()
    {
        var subject = NovoSubject();
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, subject),
            new(System.Security.Claims.ClaimTypes.Email, $"{Guid.NewGuid():N}@exemplo.com"),
            new(GoogleIdentityValidator.EmailVerifiedClaim, "true")
        };
        var info = new ExternalLoginInfo(
            new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(claims, "Google")),
            GoogleIdentityValidator.Provider, subject, GoogleIdentityValidator.Provider);
        var validator = _scope.ServiceProvider.GetRequiredService<GoogleIdentityValidator>();

        var google = await validator.ValidarAsync(info);

        Assert.True(google.EmailVerified);
    }

    // ---------------------------------------------------------------------
    // Endpoints
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Conectar_sem_credenciais_retorna_503_google_indisponivel()
    {
        var conta = await CriarContaConfirmadaAsync();
        using var cliente = await ClienteAutenticadoAsync(conta);

        var resposta = await cliente.GetAsync("/api/auth/google/conectar");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resposta.StatusCode);
        var erro = await resposta.Content.ReadFromJsonAsync<ErroResponse>();
        Assert.NotNull(erro);
        Assert.Equal("google_indisponivel", erro!.Codigo);
        Assert.False(TemCookie(resposta, ConexaoGoogleService.IntencaoCookie));
    }

    [Fact]
    public async Task Conectar_com_consentimento_pendente_e_bloqueado_pelo_gate()
    {
        // Conta válida, mas SEM aceite vigente: o middleware da Task 7 barra
        // /conectar antes mesmo do endpoint (e da checagem de credenciais).
        var conta = await CriarContaConfirmadaAsync(registrarConsentimento: false);
        using var cliente = await ClienteAutenticadoAsync(conta);

        var resposta = await cliente.GetAsync("/api/auth/google/conectar");

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        var erro = await resposta.Content.ReadFromJsonAsync<ErroResponse>();
        Assert.NotNull(erro);
        Assert.Equal("consentimento_pendente", erro!.Codigo);
        Assert.False(TemCookie(resposta, ConexaoGoogleService.IntencaoCookie));
    }

    [Fact]
    public async Task Callback_de_outro_usuario_e_recusado_e_apaga_a_intencao()
    {
        var contaA = await CriarContaConfirmadaAsync();
        var contaB = await CriarContaConfirmadaAsync();
        var valorIntencaoB = await _conexao.CriarIntencaoAsync(contaB.Id);
        using var cliente = await ClienteAutenticadoAsync(contaA, valorIntencaoB);
        var sessoesAntes = await _db.Sessoes.CountAsync();

        var resposta = await cliente.GetAsync("/api/auth/google/conectar/concluir");

        Assert.Equal(HttpStatusCode.Found, resposta.StatusCode);
        Assert.Contains("estado=google_intencao_invalida", resposta.Headers.Location!.ToString());
        Assert.True(CookieFoiApagado(resposta, ConexaoGoogleService.IntencaoCookie));
        Assert.False(TemCookie(resposta, "fisga_auth"),
            "Um callback recusado nunca emite cookie de autenticação.");
        Assert.Equal(0, await _db.UserLogins.CountAsync());
        Assert.Equal(sessoesAntes, await _db.Sessoes.CountAsync());
    }

    [Fact]
    public async Task Callback_sem_usuario_autenticado_apaga_a_intencao_e_nao_vincula()
    {
        var dono = await CriarContaConfirmadaAsync();
        var valor = await _conexao.CriarIntencaoAsync(dono.Id);
        using var cliente = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        cliente.DefaultRequestHeaders.Add("Cookie", $"{ConexaoGoogleService.IntencaoCookie}={valor}");

        var resposta = await cliente.GetAsync("/api/auth/google/conectar/concluir");

        Assert.Equal(HttpStatusCode.Found, resposta.StatusCode);
        Assert.Contains("estado=google_conexao_nao_autenticado", resposta.Headers.Location!.ToString());
        Assert.True(CookieFoiApagado(resposta, ConexaoGoogleService.IntencaoCookie));
        Assert.False(TemCookie(resposta, "fisga_auth"));
        Assert.Equal(0, await _db.UserLogins.CountAsync());
    }

    [Fact]
    public async Task Me_GoogleConectado_reflete_as_linhas_de_aspnet_user_logins()
    {
        var conta = await CriarContaConfirmadaAsync();
        using var cliente = await ClienteAutenticadoAsync(conta);

        var antes = await cliente.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.NotNull(antes);
        Assert.False(antes!.GoogleConectado);

        Assert.True((await _usuarios.AddLoginAsync(conta, Login(NovoSubject()))).Succeeded);

        var depois = await cliente.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.NotNull(depois);
        Assert.True(depois!.GoogleConectado);
    }

    // O sucesso do callback sem credenciais reais: semeamos o cookie do esquema
    // externo via IAuthenticationService.SignInAsync no host de teste. Prova que
    // o fluxo só grava o vínculo — sem novo cookie fisga_auth e sem nova Sessao.
    [Fact]
    public async Task Callback_bem_sucedido_vincula_sem_novo_cookie_de_autenticacao_nem_sessao()
    {
        var conta = await CriarContaConfirmadaAsync();
        var subject = NovoSubject();
        var valorIntencao = await _conexao.CriarIntencaoAsync(conta.Id);
        var cookieExterna = CookieExterna(subject, conta.Email!);
        using var cliente = await ClienteAutenticadoAsync(conta, valorIntencao, cookieExterna);
        var sessoesAntes = await _db.Sessoes.CountAsync();

        var resposta = await cliente.GetAsync("/api/auth/google/conectar/concluir");

        Assert.Equal(HttpStatusCode.Found, resposta.StatusCode);
        Assert.Contains("estado=google_conectado", resposta.Headers.Location!.ToString());
        Assert.True(CookieFoiApagado(resposta, ConexaoGoogleService.IntencaoCookie));
        Assert.False(TemCookie(resposta, "fisga_auth"),
            "Conectar nunca emite um novo cookie de autenticação.");

        var dono = await _usuarios.FindByLoginAsync(GoogleIdentityValidator.Provider, subject);
        Assert.NotNull(dono);
        Assert.Equal(conta.Id, dono!.Id);
        Assert.Equal(1, await _db.UserLogins.CountAsync(l =>
            l.LoginProvider == GoogleIdentityValidator.Provider && l.ProviderKey == subject));
        Assert.Equal(sessoesAntes, await _db.Sessoes.CountAsync());

        // Mesma identidade: o cookie fisga_auth original continua válido.
        var me = await cliente.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.NotNull(me);
        Assert.True(me!.Autenticado);
        Assert.Equal(conta.Id, me.Usuario!.Id);
        Assert.True(me.GoogleConectado);
    }

    [Fact]
    public async Task Me_anonimo_devolve_GoogleConectado_falso()
    {
        using var cliente = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var me = await cliente.GetFromJsonAsync<MeResponse>("/api/auth/me");

        Assert.NotNull(me);
        Assert.False(me!.Autenticado);
        Assert.False(me.GoogleConectado);
    }

    [Fact]
    public async Task Callback_com_consentimento_pendente_e_bloqueado_pelo_gate()
    {
        var conta = await CriarContaConfirmadaAsync(registrarConsentimento: false);
        var valorIntencao = await _conexao.CriarIntencaoAsync(conta.Id);
        using var cliente = await ClienteAutenticadoAsync(conta, valorIntencao);

        var resposta = await cliente.GetAsync("/api/auth/google/conectar/concluir");

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        var erro = await resposta.Content.ReadFromJsonAsync<ErroResponse>();
        Assert.NotNull(erro);
        Assert.Equal("consentimento_pendente", erro!.Codigo);
        Assert.Equal(0, await _db.UserLogins.CountAsync());
    }

    // ---------------------------------------------------------------------
    // Auxiliares
    // ---------------------------------------------------------------------

    private async Task<ApplicationUser> CriarContaConfirmadaAsync(bool registrarConsentimento = true)
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var usuario = await _cadastro.CriarComSenhaAsync(email, "Pescador", Senha);

        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.NotNull(conta);
        conta!.EmailConfirmed = true;
        Assert.True((await _usuarios.UpdateAsync(conta)).Succeeded);

        if (registrarConsentimento)
        {
            await _consentimento.RegistrarAsync(
                usuario.Id,
                _opcoesLegais.TermsVersion,
                _opcoesLegais.PrivacyVersion);
        }

        return conta;
    }

    private async Task<HttpClient> ClienteAutenticadoAsync(
        ApplicationUser conta,
        string? cookieIntencao = null,
        string? cookieExterna = null)
    {
        using var clienteLogin = _fixture.Factory.CreateClient();
        var login = await clienteLogin.PostAsJsonAsync(
            "/api/auth/entrar", new { email = conta.Email, senha = Senha });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var auth = login.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith("fisga_auth=", StringComparison.OrdinalIgnoreCase))
            .Split(';')[0];

        var cliente = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var cookies = new List<string> { auth };
        if (cookieIntencao is not null)
        {
            cookies.Add($"{ConexaoGoogleService.IntencaoCookie}={cookieIntencao}");
        }

        if (cookieExterna is not null)
        {
            cookies.Add(cookieExterna);
        }

        cliente.DefaultRequestHeaders.Add("Cookie", cookies);
        return cliente;
    }

    /// <summary>
    /// Semeia o cookie do esquema externo da Identity sem passar pelo Google:
    /// o handler de cookie grava o ticket no Set-Cookie de um HttpContext
    /// sintético, usando o mesmo anel de chaves do host de teste.
    /// </summary>
    private string CookieExterna(string subject, string email)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var autenticacao = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var contexto = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subject),
            new(ClaimTypes.Email, email),
            new(GoogleIdentityValidator.EmailVerifiedClaim, "True")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            claims, "Google", ClaimTypes.NameIdentifier, ClaimTypes.Role));
        var propriedades = new AuthenticationProperties();
        propriedades.Items["LoginProvider"] = GoogleIdentityValidator.Provider;

        autenticacao.SignInAsync(
            contexto,
            IdentityConstants.ExternalScheme,
            principal,
            propriedades).GetAwaiter().GetResult();

        var setCookie = contexto.Response.Headers["Set-Cookie"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(setCookie));
        return setCookie.Split(';')[0];
    }

    private static GoogleUsuario NovoGoogle(string subject, string email, bool verified) =>
        new(subject, Email.Criar(email), "Nome do Google", verified);

    private static UserLoginInfo Login(string subject) =>
        new(GoogleIdentityValidator.Provider, subject, GoogleIdentityValidator.Provider);

    private static string NovoSubject() => $"google-subject-{Guid.NewGuid():N}";

    private static bool TemCookie(HttpResponseMessage resposta, string nome) =>
        resposta.Headers.TryGetValues("Set-Cookie", out var cookies)
        && cookies.Any(c => c.StartsWith($"{nome}=", StringComparison.OrdinalIgnoreCase));

    private static bool CookieFoiApagado(HttpResponseMessage resposta, string nome) =>
        resposta.Headers.TryGetValues("Set-Cookie", out var cookies)
        && cookies.Any(c => c.StartsWith($"{nome}=;", StringComparison.OrdinalIgnoreCase));
}

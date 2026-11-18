using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Fisga.Api.Contracts;
using Fisga.Api.Endpoints;
using Fisga.Application.Identidade;
using Fisga.Domain.Common;
using Fisga.Domain.Identidade;
using Fisga.Infrastructure.Identity;
using Fisga.Infrastructure.Persistence;

namespace Fisga.Api.Tests;

/// <summary>
/// Login com Google sem credenciais reais: o <see cref="ExternalLoginInfo"/> é
/// montado diretamente e o serviço é exercitado. O fluxo OAuth de verdade fica
/// para o E2E com credenciais (Task 13). A ordem de resolução é segurança:
/// subjects já vinculados entram na mesma conta; e-mails só são vinculados quando
/// verificados nos DOIS lados; nunca promovemos uma conta de senha não confirmada.
/// </summary>
[Collection("api")]
public sealed class GoogleLoginTests : IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;
    private readonly IServiceScope _scope;
    private readonly FisgaDbContext _db;
    private readonly ICadastroDeUsuario _cadastro;
    private readonly UserManager<ApplicationUser> _usuarios;
    private readonly IUsuarioRepository _repositorio;
    private readonly GoogleIdentityService _servico;
    private readonly GoogleIdentityValidator _validator;

    public GoogleLoginTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _scope = fixture.Factory.Services.CreateScope();
        var sp = _scope.ServiceProvider;
        _db = sp.GetRequiredService<FisgaDbContext>();
        _cadastro = sp.GetRequiredService<ICadastroDeUsuario>();
        _usuarios = sp.GetRequiredService<UserManager<ApplicationUser>>();
        _repositorio = sp.GetRequiredService<IUsuarioRepository>();
        _servico = sp.GetRequiredService<GoogleIdentityService>();
        _validator = sp.GetRequiredService<GoogleIdentityValidator>();
    }

    public async Task InitializeAsync()
    {
        // Ordem explícita: logins, sessões e consentimentos apontam para usuários.
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
    // Serviço de linking — quatro caminhos vinculantes do brief
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Subject_ja_vinculado_entra_na_mesma_conta()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var subject = NovoSubject();
        var original = await _cadastro.GarantirUsuarioAsync(
            Email.Criar(email), "Pescador", emailVerificado: true);
        var conta = await _usuarios.FindByIdAsync(original.Id.ToString());
        Assert.NotNull(conta);
        Assert.True((await _usuarios.AddLoginAsync(conta!, Login(subject))).Succeeded);

        var entrado = await _servico.EntrarOuCriarAsync(
            new GoogleUsuario(subject, Email.Criar(email), "Nome do Google"));

        Assert.Equal(original.Id, entrado.Id);
        Assert.Equal(1, await _db.Usuarios.CountAsync(u => u.Email == Email.Criar(email)));
        Assert.Equal(1, await _db.Users.CountAsync(u => u.Email == email));
        var vinculado = await _usuarios.FindByLoginAsync("Google", subject);
        Assert.NotNull(vinculado);
        Assert.Equal(original.Id, vinculado!.Id);
    }

    [Fact]
    public async Task Subject_da_conta_A_com_email_da_conta_B_entra_na_conta_A()
    {
        var emailA = $"{Guid.NewGuid():N}@exemplo.com";
        var emailB = $"outro-{Guid.NewGuid():N}@exemplo.com";
        const string senhaB = "senha-segura-123";
        var subject = NovoSubject();

        var contaA = await _cadastro.GarantirUsuarioAsync(
            Email.Criar(emailA), "Conta A", emailVerificado: true);
        var contaB = await _cadastro.CriarComSenhaAsync(emailB, "Conta B", senhaB);
        var usuarioA = await _usuarios.FindByIdAsync(contaA.Id.ToString());
        var usuarioB = await _usuarios.FindByIdAsync(contaB.Id.ToString());
        Assert.NotNull(usuarioA);
        Assert.NotNull(usuarioB);
        Assert.True((await _usuarios.AddLoginAsync(usuarioA!, Login(subject))).Succeeded);
        var stampAntes = usuarioB!.SecurityStamp;

        // O subject decide a identidade: o e-mail da conta B não pode sequer ser
        // considerado, muito menos promover ou alterar a conta B.
        var entrado = await _servico.EntrarOuCriarAsync(
            new GoogleUsuario(subject, Email.Criar(emailB), "Nome do Google"));

        Assert.Equal(contaA.Id, entrado.Id);
        Assert.NotEqual(contaB.Id, entrado.Id);
        Assert.Equal(1, await _db.UserLogins.CountAsync(l =>
            l.LoginProvider == "Google" && l.ProviderKey == subject));
        Assert.Equal(0, await _db.UserLogins.CountAsync(l => l.UserId == contaB.Id));

        // Conta B permanece intacta: não verificada, sem login e com a mesma senha.
        var contaBDepois = await _usuarios.FindByIdAsync(contaB.Id.ToString());
        Assert.NotNull(contaBDepois);
        Assert.False(contaBDepois!.EmailConfirmed);
        Assert.Equal(stampAntes, contaBDepois.SecurityStamp);
        Assert.Empty(await _usuarios.GetLoginsAsync(contaBDepois));
        Assert.True(await _usuarios.CheckPasswordAsync(contaBDepois, senhaB));
        Assert.Null((await _db.Usuarios.SingleAsync(u => u.Id == contaB.Id)).EmailVerificadoEm);
    }

    [Fact]
    public async Task Email_verificado_existente_e_vinculado_sem_promover_nem_duplicar()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var subject = NovoSubject();
        var original = await _cadastro.GarantirUsuarioAsync(
            Email.Criar(email), "Pescador", emailVerificado: true);

        var entrado = await _servico.EntrarOuCriarAsync(
            new GoogleUsuario(subject, Email.Criar(email), null));

        Assert.Equal(original.Id, entrado.Id);
        Assert.Equal(1, await _db.Usuarios.CountAsync(u => u.Email == Email.Criar(email)));
        Assert.Equal(1, await _db.Users.CountAsync(u => u.Email == email));

        var vinculado = await _usuarios.FindByLoginAsync("Google", subject);
        Assert.NotNull(vinculado);
        Assert.Equal(original.Id, vinculado!.Id);

        var conta = await _usuarios.FindByIdAsync(original.Id.ToString());
        Assert.NotNull(conta);
        Assert.True(conta!.EmailConfirmed);
        Assert.Null(conta.PasswordHash);
        // O nome local não é sobrescrito pelo Google.
        Assert.Equal("Pescador", entrado.Nome);
    }

    [Fact]
    public async Task Email_local_nao_verificado_e_recusado_sem_alterar_estado()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        const string senha = "senha-segura-123";
        var subject = NovoSubject();
        var usuario = await _cadastro.CriarComSenhaAsync(email, "Pescador", senha);

        var excecao = await Assert.ThrowsAsync<DomainException>(
            () => _servico.EntrarOuCriarAsync(
                new GoogleUsuario(subject, Email.Criar(email), null)));

        Assert.Equal("google_email_nao_verificado_localmente", excecao.Codigo);

        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.NotNull(conta);
        Assert.False(conta!.EmailConfirmed, "A conta de senha NÃO pode ser promovida.");
        Assert.Null((await _db.Usuarios.SingleAsync(u => u.Id == usuario.Id)).EmailVerificadoEm);
        Assert.True(await _usuarios.CheckPasswordAsync(conta, senha), "A credencial de senha permanece intacta.");
        Assert.Null(await _usuarios.FindByLoginAsync("Google", subject));
        Assert.Equal(1, await _db.Usuarios.CountAsync(u => u.Email == Email.Criar(email)));
        Assert.Equal(1, await _db.Users.CountAsync(u => u.Email == email));
    }

    [Fact]
    public async Task Dominio_verificado_mas_conta_nao_confirmada_e_recusado()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var subject = NovoSubject();
        var usuario = await _cadastro.CriarComSenhaAsync(email, "Pescador", "senha-segura-123");
        usuario.VerificarEmail(DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync();

        var excecao = await Assert.ThrowsAsync<DomainException>(
            () => _servico.EntrarOuCriarAsync(new GoogleUsuario(subject, Email.Criar(email), null)));

        Assert.Equal("google_email_nao_verificado_localmente", excecao.Codigo);
        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.NotNull(conta);
        Assert.False(conta!.EmailConfirmed);
        Assert.Null(await _usuarios.FindByLoginAsync("Google", subject));
    }

    [Fact]
    public async Task Conta_confirmada_mas_dominio_nao_verificado_e_recusado()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var subject = NovoSubject();
        var usuario = await _cadastro.CriarComSenhaAsync(email, "Pescador", "senha-segura-123");
        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.NotNull(conta);
        conta!.EmailConfirmed = true;
        Assert.True((await _usuarios.UpdateAsync(conta)).Succeeded);

        var excecao = await Assert.ThrowsAsync<DomainException>(
            () => _servico.EntrarOuCriarAsync(new GoogleUsuario(subject, Email.Criar(email), null)));

        Assert.Equal("google_email_nao_verificado_localmente", excecao.Codigo);
        Assert.Null((await _db.Usuarios.SingleAsync(u => u.Id == usuario.Id)).EmailVerificadoEm);
        Assert.Null(await _usuarios.FindByLoginAsync("Google", subject));
    }

    [Fact]
    public async Task Email_novo_cria_usuario_e_conta_verificados_com_o_subject_vinculado()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var subject = NovoSubject();

        var usuario = await _servico.EntrarOuCriarAsync(
            new GoogleUsuario(subject, Email.Criar(email), "Nome do Google"));

        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.NotNull(usuario.EmailVerificadoEm);
        Assert.Equal("Nome do Google", usuario.Nome);
        Assert.NotNull(conta);
        Assert.Equal(usuario.Id, conta!.Id);
        Assert.Equal(email, conta.Email);
        Assert.Equal(email, conta.UserName);
        Assert.True(conta.EmailConfirmed);
        Assert.Null(conta.PasswordHash);

        var vinculado = await _usuarios.FindByLoginAsync("Google", subject);
        Assert.NotNull(vinculado);
        Assert.Equal(usuario.Id, vinculado!.Id);
        Assert.Equal(1, await _db.UserLogins.CountAsync(l => l.LoginProvider == "Google" && l.ProviderKey == subject));
        Assert.Equal(1, await _db.Usuarios.CountAsync(u => u.Email == Email.Criar(email)));
        Assert.Equal(1, await _db.Users.CountAsync(u => u.Email == email));
    }

    [Fact]
    public async Task Falha_no_AddLogin_recusa_sem_trocar_de_conta()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var subject = NovoSubject();
        var usuario = await _cadastro.GarantirUsuarioAsync(
            Email.Criar(email), "Pescador", emailVerificado: true);
        var outro = new ApplicationUser { Id = Guid.NewGuid(), Email = "outro@exemplo.com" };

        // Corrida: a 1ª leitura não vê dono, o AddLogin falha (outro request venceu)
        // e a 2ª leitura já encontra o subject na conta alheia.
        var sp = _scope.ServiceProvider;
        var store = new StoreQuePerdeuACorrida(_db, new IdentityErrorDescriber(), outro);
        var usuariosComFalha = new UserManager<ApplicationUser>(
            store,
            sp.GetRequiredService<IOptions<IdentityOptions>>(),
            sp.GetRequiredService<IPasswordHasher<ApplicationUser>>(),
            sp.GetRequiredService<IEnumerable<IUserValidator<ApplicationUser>>>(),
            sp.GetRequiredService<IEnumerable<IPasswordValidator<ApplicationUser>>>(),
            sp.GetRequiredService<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            sp,
            sp.GetRequiredService<ILogger<UserManager<ApplicationUser>>>());
        var servicoComFalha = new GoogleIdentityService(usuariosComFalha, _repositorio, _cadastro);

        var excecao = await Assert.ThrowsAsync<DomainException>(
            () => servicoComFalha.EntrarOuCriarAsync(
                new GoogleUsuario(subject, Email.Criar(email), null)));

        Assert.Equal("google_vinculo_falhou", excecao.Codigo);
        Assert.Null(await _usuarios.FindByLoginAsync("Google", subject));
        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.NotNull(conta);
        Assert.True(conta!.EmailConfirmed);

        // Uma falha de infraestrutura sem dono para o subject NÃO é traduzida.
        var storeSemDono = new StoreQueFalhaSemDono(_db, new IdentityErrorDescriber());
        var usuariosSemDono = new UserManager<ApplicationUser>(
            storeSemDono,
            sp.GetRequiredService<IOptions<IdentityOptions>>(),
            sp.GetRequiredService<IPasswordHasher<ApplicationUser>>(),
            sp.GetRequiredService<IEnumerable<IUserValidator<ApplicationUser>>>(),
            sp.GetRequiredService<IEnumerable<IPasswordValidator<ApplicationUser>>>(),
            sp.GetRequiredService<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            sp,
            sp.GetRequiredService<ILogger<UserManager<ApplicationUser>>>());
        var servicoSemDono = new GoogleIdentityService(usuariosSemDono, _repositorio, _cadastro);

        var infra = await Assert.ThrowsAsync<DbUpdateException>(
            () => servicoSemDono.EntrarOuCriarAsync(
                new GoogleUsuario(NovoSubject(), Email.Criar(email), null)));

        Assert.Contains("infra", infra.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------
    // Validador do ExternalLoginInfo
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Validador_aceita_info_do_google()
    {
        var subject = NovoSubject();
        var info = NovaInfo("Google", subject, "Usuario@Exemplo.com", emailVerificado: true, nome: "  Pescador  ");

        var google = await _validator.ValidarAsync(info);

        Assert.Equal(subject, google.Subject);
        Assert.Equal("usuario@exemplo.com", google.Email.Valor);
        Assert.Equal("Pescador", google.Nome);
    }

    // O MapJsonKey do ASP.NET Core converte o boolean do userinfo do Google com
    // JsonElement.ToString(): "True"/"False" (capitalizado). Os dois casos precisam
    // ser aceitos/recusados para uma futura regressão case-sensitive ser detectada.
    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    public async Task Validador_aceita_email_verificado_em_qualquer_caixa(string claimVerificado)
    {
        var info = NovaInfo(
            "Google", NovoSubject(), "usuario@exemplo.com",
            emailVerificado: true, claimVerificado: claimVerificado);

        var google = await _validator.ValidarAsync(info);

        Assert.Equal("usuario@exemplo.com", google.Email.Valor);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    public async Task Validador_recusa_email_nao_verificado_pelo_google(string claimVerificado)
    {
        var info = NovaInfo(
            "Google", NovoSubject(), "usuario@exemplo.com",
            emailVerificado: false, claimVerificado: claimVerificado);

        var excecao = await Assert.ThrowsAsync<DomainException>(() => _validator.ValidarAsync(info));

        Assert.Equal("google_email_nao_verificado", excecao.Codigo);
    }

    [Theory]
    [InlineData("Facebook")]
    [InlineData("")]
    public async Task Validador_recusa_provedor_diferente_de_google(string provider)
    {
        var info = NovaInfo(provider, NovoSubject(), "usuario@exemplo.com", emailVerificado: true);

        var excecao = await Assert.ThrowsAsync<DomainException>(() => _validator.ValidarAsync(info));

        Assert.Equal("google_provider_invalido", excecao.Codigo);
    }

    [Fact]
    public async Task Validador_recusa_subject_vazio()
    {
        var info = NovaInfo("Google", string.Empty, "usuario@exemplo.com", emailVerificado: true);

        var excecao = await Assert.ThrowsAsync<DomainException>(() => _validator.ValidarAsync(info));

        Assert.Equal("google_subject_invalido", excecao.Codigo);
    }

    [Fact]
    public async Task Validador_recusa_email_ausente()
    {
        var info = NovaInfo("Google", NovoSubject(), null, emailVerificado: true);

        var excecao = await Assert.ThrowsAsync<DomainException>(() => _validator.ValidarAsync(info));

        Assert.Equal("google_email_ausente", excecao.Codigo);
    }

    // ---------------------------------------------------------------------
    // Endpoints de disponibilidade / callback sem sessão externa
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Disponivel_retorna_false_sem_credenciais()
    {
        using var cliente = _fixture.Factory.CreateClient();

        var corpo = await cliente.GetFromJsonAsync<GoogleDisponivelResponse>("/api/auth/google/disponivel");

        Assert.NotNull(corpo);
        Assert.False(corpo!.Disponivel);
    }

    [Fact]
    public async Task Iniciar_sem_credenciais_retorna_503_google_indisponivel()
    {
        using var cliente = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var resposta = await cliente.GetAsync("/api/auth/google/iniciar");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resposta.StatusCode);
        var erro = await resposta.Content.ReadFromJsonAsync<ErroResponse>();
        Assert.NotNull(erro);
        Assert.Equal("google_indisponivel", erro!.Codigo);
    }

    [Fact]
    public async Task Concluir_sem_sessao_externa_redireciona_para_falha()
    {
        using var cliente = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var resposta = await cliente.GetAsync("/api/auth/google/concluir");

        Assert.Equal(HttpStatusCode.Found, resposta.StatusCode);
        Assert.Contains("estado=google_sessao_externa_invalida", resposta.Headers.Location!.ToString());
        Assert.False(resposta.Headers.TryGetValues("Set-Cookie", out var cookies)
                     && cookies.Any(c => c.StartsWith("fisga_auth=", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Concluir_com_sessao_externa_entra_e_apaga_cookie_legado()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var cookieExterna = CookieExterna(NovoSubject(), email);

        using var cliente = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        cliente.DefaultRequestHeaders.Add("Cookie", cookieExterna);

        var resposta = await cliente.GetAsync("/api/auth/google/concluir");

        Assert.Equal(HttpStatusCode.Found, resposta.StatusCode);
        Assert.Contains("/boas-vindas?novo=1", resposta.Headers.Location!.ToString());

        Assert.True(resposta.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, c => c.StartsWith("fisga_auth=", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cookies!, c => c.StartsWith("fisga_sessao=;", StringComparison.OrdinalIgnoreCase));

        var me = await cliente.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.True(me!.Autenticado);
        Assert.Equal(email, me.Usuario!.Email);
    }

    [Fact]
    public void Deteccao_de_credenciais_respeita_client_id_e_secret()
    {
        static IConfiguration Config(string? id, string? segredo) =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Google:ClientId"] = id,
                ["Google:ClientSecret"] = segredo
            }).Build();

        Assert.False(AuthEndpoints.GoogleConfigurado(Config(null, null)));
        Assert.False(AuthEndpoints.GoogleConfigurado(Config("id", "")));
        Assert.False(AuthEndpoints.GoogleConfigurado(Config("  ", "segredo")));
        Assert.True(AuthEndpoints.GoogleConfigurado(Config("id", "segredo")));
    }

    // ---------------------------------------------------------------------
    // Auxiliares
    // ---------------------------------------------------------------------

    private static string NovoSubject() => $"google-subject-{Guid.NewGuid():N}";

    /// <summary>
    /// Semeia o cookie do esquema externo da Identity, como se o handshake do
    /// Google tivesse terminado. Usa o mesmo anel de chaves do host de teste.
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
            new(GoogleIdentityValidator.EmailVerifiedClaim, "true")
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

    private static UserLoginInfo Login(string subject) =>
        new("Google", subject, "Google");

    private static ExternalLoginInfo NovaInfo(
        string provider,
        string subject,
        string? email,
        bool emailVerificado,
        string? nome = null,
        string? claimVerificado = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subject),
            new("email_verified", claimVerificado ?? (emailVerificado ? "true" : "false"))
        };
        if (email is not null)
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        if (nome is not null)
        {
            claims.Add(new Claim(ClaimTypes.Name, nome));
        }

        return new ExternalLoginInfo(
            new ClaimsPrincipal(new ClaimsIdentity(claims, "Google")), provider, subject, "Google");
    }

    // A 1ª consulta não encontra dono; depois que o AddLogin falha, o subject já
    // pertence a outro usuário — exatamente a corrida que o serviço precisa recusar.
    private sealed class StoreQuePerdeuACorrida(
        FisgaDbContext db,
        IdentityErrorDescriber describer,
        ApplicationUser novoDono)
        : UserStore<ApplicationUser, IdentityRole<Guid>, FisgaDbContext, Guid>(db, describer)
    {
        private bool _jaTentouVincular;

        public override Task<ApplicationUser?> FindByLoginAsync(
            string loginProvider,
            string providerKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_jaTentouVincular ? novoDono : null);

        public override Task AddLoginAsync(
            ApplicationUser user,
            UserLoginInfo login,
            CancellationToken cancellationToken = default)
        {
            _jaTentouVincular = true;
            return Task.FromException(new DbUpdateException("Subject duplicado."));
        }
    }

    // Falha de infraestrutura sem dono para o subject: deve propagar, não virar
    // google_vinculo_falhou.
    private sealed class StoreQueFalhaSemDono(
        FisgaDbContext db,
        IdentityErrorDescriber describer)
        : UserStore<ApplicationUser, IdentityRole<Guid>, FisgaDbContext, Guid>(db, describer)
    {
        public override Task<ApplicationUser?> FindByLoginAsync(
            string loginProvider,
            string providerKey,
            CancellationToken cancellationToken = default) => Task.FromResult<ApplicationUser?>(null);

        public override Task AddLoginAsync(
            ApplicationUser user,
            UserLoginInfo login,
            CancellationToken cancellationToken = default) =>
            Task.FromException(new DbUpdateException("Falha de infraestrutura no banco."));
    }
}

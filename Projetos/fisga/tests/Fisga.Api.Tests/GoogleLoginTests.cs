using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
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

        // Simula a corrida em que outro usuário acabou de vincular o mesmo subject:
        // FindByLoginAsync não o vê, mas a store recusa o AddLoginAsync.
        var sp = _scope.ServiceProvider;
        var store = new StoreQueFalhaAoVincular(_db, new IdentityErrorDescriber());
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

    [Fact]
    public async Task Validador_recusa_email_nao_verificado_pelo_google()
    {
        var info = NovaInfo("Google", NovoSubject(), "usuario@exemplo.com", emailVerificado: false);

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

    private static UserLoginInfo Login(string subject) =>
        new("Google", subject, "Google");

    private static ExternalLoginInfo NovaInfo(
        string provider,
        string subject,
        string? email,
        bool emailVerificado,
        string? nome = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subject),
            new("email_verified", emailVerificado ? "true" : "false")
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

    private sealed class StoreQueFalhaAoVincular(
        FisgaDbContext db,
        IdentityErrorDescriber describer)
        : UserStore<ApplicationUser, IdentityRole<Guid>, FisgaDbContext, Guid>(db, describer)
    {
        public override Task AddLoginAsync(
            ApplicationUser user,
            UserLoginInfo login,
            CancellationToken cancellationToken = default) =>
            Task.FromException(new DbUpdateException("Subject já vinculado a outro usuário."));
    }
}

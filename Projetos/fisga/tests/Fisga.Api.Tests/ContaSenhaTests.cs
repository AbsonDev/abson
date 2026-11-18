using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fisga.Api.Contracts;
using Fisga.Application.Abstractions;
using Fisga.Application.Identidade;
using Fisga.Application.Seguranca;
using Fisga.Domain.Common;
using Fisga.Domain.Identidade;
using Fisga.Infrastructure.Identity;
using Fisga.Infrastructure.Persistence;

namespace Fisga.Api.Tests;

[Collection("api")]
public sealed class ContaSenhaTests : IAsyncLifetime
{
    private const string Senha = "senha-segura-123";
    private const string NovaSenha = "outra-senha-456";

    private readonly ApiTestFixture _fixture;
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;
    private readonly FisgaDbContext _db;
    private readonly ICadastroDeUsuario _cadastro;
    private readonly UserManager<ApplicationUser> _usuarios;
    private readonly ConsentimentoService _consentimento;
    private readonly LegalOptions _opcoesLegais;
    private readonly GoogleIdentityService _google;

    public ContaSenhaTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
        _scope = fixture.Factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<FisgaDbContext>();
        _cadastro = _scope.ServiceProvider.GetRequiredService<ICadastroDeUsuario>();
        _usuarios = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        _consentimento = _scope.ServiceProvider.GetRequiredService<ConsentimentoService>();
        _opcoesLegais = _scope.ServiceProvider.GetRequiredService<LegalOptions>();
        _google = _scope.ServiceProvider.GetRequiredService<GoogleIdentityService>();
    }

    public async Task InitializeAsync()
    {
        await _db.Sessoes.ExecuteDeleteAsync();
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

    [Fact]
    public async Task Adicionar_senha_em_conta_sem_senha_revoga_as_outras_sessoes_e_mantem_a_atual()
    {
        var conta = await CriarContaSemSenhaAsync();
        Assert.False(await _usuarios.HasPasswordAsync(conta));

        using var atual = await ClienteAutenticadoAsync(conta, "Atual");
        using var outro = await ClienteAutenticadoAsync(conta, "Outro");

        Assert.False(await LerTemSenhaAsync(atual));

        var resposta = await atual.PostAsJsonAsync(
            "/api/conta/senha",
            new { novaSenha = NovaSenha });

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);

        conta = (await _usuarios.FindByIdAsync(conta.Id.ToString()))!;
        await _db.Entry(conta).ReloadAsync();
        Assert.True(await _usuarios.HasPasswordAsync(conta));
        Assert.True(await _usuarios.CheckPasswordAsync(conta, NovaSenha));

        var meAtual = await atual.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.True(meAtual!.Autenticado);
        Assert.True(await LerTemSenhaAsync(atual));

        var meOutro = await outro.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.False(meOutro!.Autenticado);

        var sessoes = await _db.Sessoes.AsNoTracking()
            .Where(s => s.UsuarioId == conta.Id)
            .ToListAsync();
        Assert.Null(sessoes.Single(s => s.UserAgent == "Atual").RevogadaEm);
        Assert.NotNull(sessoes.Single(s => s.UserAgent == "Outro").RevogadaEm);
    }

    [Fact]
    public async Task Alterar_senha_com_senha_atual_correta_troca_a_senha_e_revoga_outras_sessoes()
    {
        var conta = await CriarContaComSenhaAsync();
        using var atual = await ClienteAutenticadoAsync(conta, "Atual");
        using var outro = await ClienteAutenticadoAsync(conta, "Outro");

        var resposta = await atual.PostAsJsonAsync(
            "/api/conta/senha",
            new { senhaAtual = Senha, novaSenha = NovaSenha });

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);

        conta = (await _usuarios.FindByIdAsync(conta.Id.ToString()))!;
        await _db.Entry(conta).ReloadAsync();
        Assert.False(await _usuarios.CheckPasswordAsync(conta, Senha));
        Assert.True(await _usuarios.CheckPasswordAsync(conta, NovaSenha));

        Assert.True((await atual.GetFromJsonAsync<MeResponse>("/api/auth/me"))!.Autenticado);
        Assert.False((await outro.GetFromJsonAsync<MeResponse>("/api/auth/me"))!.Autenticado);

        var antigo = await _client.PostAsJsonAsync(
            "/api/auth/entrar",
            new { email = conta.Email, senha = Senha });
        Assert.False((await antigo.Content.ReadFromJsonAsync<EntrarResponse>())!.Autenticado);

        var novo = await _client.PostAsJsonAsync(
            "/api/auth/entrar",
            new { email = conta.Email, senha = NovaSenha });
        Assert.True((await novo.Content.ReadFromJsonAsync<EntrarResponse>())!.Autenticado);
    }

    [Fact]
    public async Task Senha_atual_errada_devolve_erro_generico_e_nao_altera_nada()
    {
        var conta = await CriarContaComSenhaAsync();
        using var atual = await ClienteAutenticadoAsync(conta, "Atual");
        using var outro = await ClienteAutenticadoAsync(conta, "Outro");

        var resposta = await atual.PostAsJsonAsync(
            "/api/conta/senha",
            new { senhaAtual = "senha-que-nao-e", novaSenha = NovaSenha });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erro = await resposta.Content.ReadFromJsonAsync<ErroResponse>();
        Assert.NotNull(erro);
        Assert.Equal("senha_atual_incorreta", erro!.Codigo);
        Assert.DoesNotContain("senha-que-nao-e", erro.Detalhe);

        conta = (await _usuarios.FindByIdAsync(conta.Id.ToString()))!;
        Assert.True(await _usuarios.CheckPasswordAsync(conta, Senha));
        Assert.False(await _usuarios.CheckPasswordAsync(conta, NovaSenha));
        Assert.True((await outro.GetFromJsonAsync<MeResponse>("/api/auth/me"))!.Autenticado);
    }

    [Fact]
    public async Task Senha_atual_ausente_quando_a_conta_ja_tem_senha_e_recusada()
    {
        var conta = await CriarContaComSenhaAsync();
        using var atual = await ClienteAutenticadoAsync(conta, "Atual");

        var resposta = await atual.PostAsJsonAsync(
            "/api/conta/senha",
            new { novaSenha = NovaSenha });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erro = await resposta.Content.ReadFromJsonAsync<ErroResponse>();
        Assert.Equal("senha_atual_incorreta", erro!.Codigo);

        conta = (await _usuarios.FindByIdAsync(conta.Id.ToString()))!;
        Assert.True(await _usuarios.CheckPasswordAsync(conta, Senha));
    }

    [Theory]
    [InlineData("123456789", "senha_curta")]
    [InlineData("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789", "senha_longa")]
    public async Task Nova_senha_fora_do_limite_e_recusada_antes_da_identity(
        string novaSenha,
        string codigoEsperado)
    {
        var conta = await CriarContaSemSenhaAsync();
        using var atual = await ClienteAutenticadoAsync(conta, "Atual");

        var resposta = await atual.PostAsJsonAsync(
            "/api/conta/senha",
            new { novaSenha });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erro = await resposta.Content.ReadFromJsonAsync<ErroResponse>();
        Assert.Equal(codigoEsperado, erro!.Codigo);

        conta = (await _usuarios.FindByIdAsync(conta.Id.ToString()))!;
        Assert.False(await _usuarios.HasPasswordAsync(conta));
    }

    [Theory]
    [InlineData(9, "senha_curta")]
    [InlineData(129, "senha_longa")]
    public async Task Alterar_senha_fora_do_limite_e_recusada_antes_da_identity(
        int tamanho,
        string codigoEsperado)
    {
        var conta = await CriarContaComSenhaAsync();
        using var atual = await ClienteAutenticadoAsync(conta, "Atual");
        var novaSenha = new string('a', tamanho);

        var resposta = await atual.PostAsJsonAsync(
            "/api/conta/senha",
            new { senhaAtual = Senha, novaSenha });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erro = await resposta.Content.ReadFromJsonAsync<ErroResponse>();
        Assert.Equal(codigoEsperado, erro!.Codigo);

        conta = (await _usuarios.FindByIdAsync(conta.Id.ToString()))!;
        await _db.Entry(conta).ReloadAsync();
        Assert.True(await _usuarios.CheckPasswordAsync(conta, Senha));
        Assert.False(await _usuarios.CheckPasswordAsync(conta, novaSenha));
    }

    [Fact]
    public async Task Cookie_copiado_antes_da_troca_de_senha_deixa_de_autenticar()
    {
        var conta = await CriarContaComSenhaAsync();
        using var atual = await ClienteAutenticadoAsync(conta, "Atual");
        var cookieAntigo = atual.DefaultRequestHeaders.GetValues("Cookie").Single();

        using var roubado = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
        roubado.DefaultRequestHeaders.Add("Cookie", cookieAntigo);

        var troca = await atual.PostAsJsonAsync(
            "/api/conta/senha",
            new { senhaAtual = Senha, novaSenha = NovaSenha });
        Assert.Equal(HttpStatusCode.NoContent, troca.StatusCode);

        var cookieNovo = troca.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith("fisga_auth=", StringComparison.OrdinalIgnoreCase))
            .Split(';')[0];
        atual.DefaultRequestHeaders.Remove("Cookie");
        atual.DefaultRequestHeaders.Add("Cookie", cookieNovo);

        var respostaRoubada = await roubado.GetAsync("/api/conta/sessoes");
        Assert.Equal(HttpStatusCode.Unauthorized, respostaRoubada.StatusCode);

        var respostaAtual = await atual.GetAsync("/api/conta/sessoes");
        Assert.Equal(HttpStatusCode.OK, respostaAtual.StatusCode);
    }

    [Fact]
    public async Task Endpoint_exige_autenticacao()
    {
        var resposta = await _client.PostAsJsonAsync(
            "/api/conta/senha",
            new { novaSenha = NovaSenha });

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        var erro = await resposta.Content.ReadFromJsonAsync<ErroResponse>();
        Assert.Equal("nao_autenticado", erro!.Codigo);
    }

    [Fact]
    public async Task Endpoint_respeita_o_gate_de_consentimento()
    {
        var conta = await CriarContaSemSenhaAsync(registrarConsentimento: false);
        using var atual = await ClienteAutenticadoAsync(conta, "Atual");

        var resposta = await atual.PostAsJsonAsync(
            "/api/conta/senha",
            new { novaSenha = NovaSenha });

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        var erro = await resposta.Content.ReadFromJsonAsync<ErroResponse>();
        Assert.Equal("consentimento_pendente", erro!.Codigo);

        conta = (await _usuarios.FindByIdAsync(conta.Id.ToString()))!;
        Assert.False(await _usuarios.HasPasswordAsync(conta));
    }

    [Fact]
    public async Task Me_informa_se_a_conta_tem_senha()
    {
        var semSenha = await CriarContaSemSenhaAsync();
        using var clienteSemSenha = await ClienteAutenticadoAsync(semSenha, "Sem-Senha");
        Assert.False(await LerTemSenhaAsync(clienteSemSenha));

        var comSenha = await CriarContaComSenhaAsync();
        using var clienteComSenha = await ClienteAutenticadoAsync(comSenha, "Com-Senha");
        Assert.True(await LerTemSenhaAsync(clienteComSenha));
    }

    // ---------------------------------------------------------------------
    // Auxiliares
    // ---------------------------------------------------------------------

    private async Task<ApplicationUser> CriarContaComSenhaAsync()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var usuario = await _cadastro.CriarComSenhaAsync(email, "Pescador", Senha);

        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.NotNull(conta);
        conta!.EmailConfirmed = true;
        Assert.True((await _usuarios.UpdateAsync(conta)).Succeeded);

        await RegistrarConsentimentoAsync(usuario.Id);
        return conta;
    }

    private async Task<ApplicationUser> CriarContaSemSenhaAsync(bool registrarConsentimento = true)
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var usuario = await _google.EntrarOuCriarAsync(
            new GoogleUsuario(
                $"subject-{Guid.NewGuid():N}",
                Email.Criar(email),
                "Pescador",
                EmailVerified: true));

        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.NotNull(conta);
        Assert.False(await _usuarios.HasPasswordAsync(conta!));

        if (registrarConsentimento)
        {
            await RegistrarConsentimentoAsync(usuario.Id);
        }

        return conta!;
    }

    private Task RegistrarConsentimentoAsync(Guid usuarioId) =>
        _consentimento.RegistrarAsync(
            usuarioId,
            _opcoesLegais.TermsVersion,
            _opcoesLegais.PrivacyVersion);

    /// <summary>
    /// Emite um cookie da Identity e cria a linha de sessão para uma conta que
    /// pode não ter senha (o login por senha não existe nesse caso). O cookie é
    /// produzido por um HttpContext sintético no mesmo anel de chaves do host.
    /// </summary>
    private async Task<HttpClient> ClienteAutenticadoAsync(ApplicationUser conta, string userAgent)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var relogio = scope.ServiceProvider.GetRequiredService<IRelogio>();
        var sessoes = scope.ServiceProvider.GetRequiredService<ISessaoRepository>();
        var unidade = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var fabrica = scope.ServiceProvider
            .GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var autenticacao = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

        var sessaoId = Guid.NewGuid();
        sessoes.Adicionar(Sessao.Criar(
            sessaoId,
            conta.Id,
            TokensSeguros.Hash(sessaoId.ToString()),
            userAgent,
            ipHash: null,
            relogio.Agora,
            TimeSpan.FromDays(7)));
        await unidade.SalvarAsync();

        var principal = await fabrica.CreateAsync(conta);
        Assert.True(principal.Identity is ClaimsIdentity);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim("sessao_id", sessaoId.ToString()));

        var contexto = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        await autenticacao.SignInAsync(
            contexto,
            IdentityConstants.ApplicationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });

        var setCookie = contexto.Response.Headers.SetCookie.ToString();
        Assert.False(string.IsNullOrWhiteSpace(setCookie));

        var cliente = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        cliente.DefaultRequestHeaders.Add("Cookie", setCookie.Split(';')[0]);
        return cliente;
    }

    private static async Task<bool?> LerTemSenhaAsync(HttpClient cliente)
    {
        var resposta = await cliente.GetAsync("/api/auth/me");
        using var documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return documento.RootElement.TryGetProperty("temSenha", out var propriedade)
            ? propriedade.GetBoolean()
            : null;
    }
}

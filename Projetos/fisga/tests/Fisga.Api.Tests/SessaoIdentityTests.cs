using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fisga.Api.Contracts;
using Fisga.Application.Identidade;
using Fisga.Application.Seguranca;
using Fisga.Domain.Identidade;
using Fisga.Infrastructure.Identity;
using Fisga.Infrastructure.Persistence;

namespace Fisga.Api.Tests;

[Collection("api")]
public sealed class SessaoIdentityTests : IAsyncLifetime
{
    private const string Senha = "senha-segura-123";

    private readonly ApiTestFixture _fixture;
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;
    private readonly FisgaDbContext _db;
    private readonly ICadastroDeUsuario _cadastro;
    private readonly UserManager<ApplicationUser> _usuarios;
    private readonly ConsentimentoService _consentimento;
    private readonly LegalOptions _opcoesLegais;

    public SessaoIdentityTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
        _scope = fixture.Factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<FisgaDbContext>();
        _cadastro = _scope.ServiceProvider.GetRequiredService<ICadastroDeUsuario>();
        _usuarios = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        _consentimento = _scope.ServiceProvider.GetRequiredService<ConsentimentoService>();
        _opcoesLegais = _scope.ServiceProvider.GetRequiredService<LegalOptions>();
    }

    public async Task InitializeAsync()
    {
        // A ordem explícita evita depender do cascade do FK sessoes -> usuarios.
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
    public async Task Login_valido_cria_sessao_e_me_fica_autenticado()
    {
        var usuario = await CriarUsuarioConfirmadoAsync();

        await EntrarAsync(_client, usuario.Email.Valor);

        var sessao = await _db.Sessoes.SingleAsync(s => s.UsuarioId == usuario.Id);
        Assert.Equal(64, sessao.TokenHash.Length);
        Assert.Equal(TokensSeguros.Hash(sessao.Id.ToString()), sessao.TokenHash);
        Assert.True(sessao.EstaAtiva(DateTimeOffset.UtcNow));

        var me = await _client.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.NotNull(me);
        Assert.True(me!.Autenticado);
        Assert.Equal(usuario.Id, me.Usuario!.Id);
    }

    [Fact]
    public async Task Sessao_revogada_no_banco_invalida_cookie_e_protege_endpoint()
    {
        var usuario = await CriarUsuarioConfirmadoAsync();
        await EntrarAsync(_client, usuario.Email.Valor);
        var sessao = await _db.Sessoes.SingleAsync(s => s.UsuarioId == usuario.Id);

        await _db.Sessoes.Where(s => s.Id == sessao.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevogadaEm, DateTimeOffset.UtcNow));

        var respostaMe = await _client.GetAsync("/api/auth/me");
        var me = await respostaMe.Content.ReadFromJsonAsync<MeResponse>();
        Assert.False(me!.Autenticado);
        Assert.True(
            CookieDeAutenticacaoFoiLimpa(respostaMe),
            "Uma sessão revogada deve limpar o cookie fisga_auth.");

        var protegido = await _client.GetAsync("/api/conta/sessoes");
        Assert.Equal(HttpStatusCode.Unauthorized, protegido.StatusCode);
    }

    [Fact]
    public async Task Sessao_expirada_e_tratada_como_invalida()
    {
        var usuario = await CriarUsuarioConfirmadoAsync();
        await EntrarAsync(_client, usuario.Email.Valor);
        var sessao = await _db.Sessoes.SingleAsync(s => s.UsuarioId == usuario.Id);

        await _db.Sessoes.Where(s => s.Id == sessao.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiraEm, DateTimeOffset.UtcNow.AddDays(-1)));

        var respostaMe = await _client.GetAsync("/api/auth/me");
        var me = await respostaMe.Content.ReadFromJsonAsync<MeResponse>();
        Assert.False(me!.Autenticado);

        var protegido = await _client.GetAsync("/api/conta/sessoes");
        Assert.Equal(HttpStatusCode.Unauthorized, protegido.StatusCode);
    }

    [Fact]
    public async Task Sessao_de_outro_usuario_nao_autentica()
    {
        var usuarioA = await CriarUsuarioConfirmadoAsync();
        var usuarioB = await CriarUsuarioConfirmadoAsync();
        await EntrarAsync(_client, usuarioA.Email.Valor);
        var sessao = await _db.Sessoes.SingleAsync(s => s.UsuarioId == usuarioA.Id);

        // O cookie mantém NameIdentifier = A, mas sessao_id passa a apontar para
        // uma linha cujo UsuarioId é B. O middleware não deve definir Items.
        await _db.Sessoes.Where(s => s.Id == sessao.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsuarioId, usuarioB.Id));

        var respostaMe = await _client.GetAsync("/api/auth/me");
        var me = await respostaMe.Content.ReadFromJsonAsync<MeResponse>();
        Assert.False(me!.Autenticado);
        Assert.True(CookieDeAutenticacaoFoiLimpa(respostaMe));

        var protegido = await _client.GetAsync("/api/conta/sessoes");
        Assert.Equal(HttpStatusCode.Unauthorized, protegido.StatusCode);
    }

    [Fact]
    public async Task Sair_revoga_apenas_a_sessao_atual_e_limpa_o_cookie()
    {
        var usuario = await CriarUsuarioConfirmadoAsync();
        using var clienteA = CriarCliente("Cliente-A");
        using var clienteB = CriarCliente("Cliente-B");
        await EntrarAsync(clienteA, usuario.Email.Valor);
        await EntrarAsync(clienteB, usuario.Email.Valor);

        var sessoes = await _db.Sessoes.AsNoTracking().Where(s => s.UsuarioId == usuario.Id).ToListAsync();
        Assert.Equal(2, sessoes.Count);
        var sessaoA = sessoes.Single(s => s.UserAgent == "Cliente-A");
        var sessaoB = sessoes.Single(s => s.UserAgent == "Cliente-B");

        var resposta = await clienteA.PostAsync("/api/auth/sair", null);
        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.True(CookieDeAutenticacaoFoiLimpa(resposta));

        var depoisA = await _db.Sessoes.AsNoTracking().SingleAsync(s => s.Id == sessaoA.Id);
        var depoisB = await _db.Sessoes.AsNoTracking().SingleAsync(s => s.Id == sessaoB.Id);
        Assert.NotNull(depoisA.RevogadaEm);
        Assert.Null(depoisB.RevogadaEm);

        var meA = await clienteA.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.False(meA!.Autenticado);
        var meB = await clienteB.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.True(meB!.Autenticado);
    }

    [Fact]
    public async Task Sair_de_todos_revoga_todas_as_sessoes_e_rotaciona_security_stamp()
    {
        var usuario = await CriarUsuarioConfirmadoAsync();
        using var clienteA = CriarCliente("Cliente-A");
        using var clienteB = CriarCliente("Cliente-B");
        await EntrarAsync(clienteA, usuario.Email.Valor);
        await EntrarAsync(clienteB, usuario.Email.Valor);

        var stampAntes = (await _db.Users.AsNoTracking().SingleAsync(u => u.Id == usuario.Id)).SecurityStamp;

        var resposta = await clienteA.PostAsync("/api/auth/sair-de-todos", null);
        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.True(CookieDeAutenticacaoFoiLimpa(resposta));

        var sessoes = await _db.Sessoes.AsNoTracking().Where(s => s.UsuarioId == usuario.Id).ToListAsync();
        Assert.Equal(2, sessoes.Count);
        Assert.All(sessoes, s => Assert.NotNull(s.RevogadaEm));

        var stampDepois = (await _db.Users.AsNoTracking().SingleAsync(u => u.Id == usuario.Id)).SecurityStamp;
        Assert.NotEqual(stampAntes, stampDepois);

        var meB = await clienteB.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.False(meB!.Autenticado);
        var protegido = await clienteB.GetAsync("/api/conta/sessoes");
        Assert.Equal(HttpStatusCode.Unauthorized, protegido.StatusCode);
    }

    [Fact]
    public async Task Listar_sessoes_marca_apenas_a_sessao_atual()
    {
        var usuario = await CriarUsuarioConfirmadoAsync();
        using var clienteA = CriarCliente("Cliente-A");
        using var clienteB = CriarCliente("Cliente-B");
        await EntrarAsync(clienteA, usuario.Email.Valor);
        await EntrarAsync(clienteB, usuario.Email.Valor);

        var sessoes = await _db.Sessoes.AsNoTracking().Where(s => s.UsuarioId == usuario.Id).ToListAsync();
        var sessaoA = sessoes.Single(s => s.UserAgent == "Cliente-A");

        var lista = await clienteA.GetFromJsonAsync<List<SessaoResponse>>("/api/conta/sessoes");
        Assert.NotNull(lista);
        Assert.Equal(2, lista!.Count);
        Assert.Single(lista, s => s.Atual);
        Assert.Equal(sessaoA.Id, lista.Single(s => s.Atual).Id);
    }

    [Fact]
    public async Task Excluir_conta_limpa_tambem_o_cookie_fisga_auth()
    {
        var usuario = await CriarUsuarioConfirmadoAsync();
        await EntrarAsync(_client, usuario.Email.Valor);

        using var requisicao = new HttpRequestMessage(HttpMethod.Delete, "/api/conta")
        {
            Content = JsonContent.Create(new { confirmacao = "EXCLUIR" })
        };
        var resposta = await _client.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.True(CookieDeAutenticacaoFoiLimpa(resposta),
            "A exclusão da conta deve limpar o cookie ativo fisga_auth.");
    }

    private HttpClient CriarCliente(string userAgent)
    {
        var cliente = _fixture.Factory.CreateClient();
        cliente.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        return cliente;
    }

    private async Task<Usuario> CriarUsuarioConfirmadoAsync()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var usuario = await _cadastro.CriarComSenhaAsync(email, "Pescador", Senha);

        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        conta!.EmailConfirmed = true;
        await _usuarios.UpdateAsync(conta);

        // Estes testes exercitam rotas de conta protegidas; o aceite vigente é
        // pré-requisito do middleware de consentimento (Task 7).
        await _consentimento.RegistrarAsync(
            usuario.Id,
            _opcoesLegais.TermsVersion,
            _opcoesLegais.PrivacyVersion);

        return usuario;
    }

    private static async Task<HttpResponseMessage> EntrarAsync(HttpClient cliente, string email)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/auth/entrar", new { email, senha = Senha });
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<EntrarResponse>();
        Assert.True(corpo!.Autenticado);
        return resposta;
    }

    private static bool CookieDeAutenticacaoFoiLimpa(HttpResponseMessage resposta) =>
        resposta.Headers.TryGetValues("Set-Cookie", out var cookies)
        && cookies.Any(c => c.StartsWith("fisga_auth=;", StringComparison.OrdinalIgnoreCase));
}

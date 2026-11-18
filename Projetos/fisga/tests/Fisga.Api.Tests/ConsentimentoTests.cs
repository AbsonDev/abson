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

/// <summary>
/// Consentimento formal: a versão vigente é lida do banco a cada requisição
/// autenticada, sem claims de versão no cookie e sem reemissão de cookie.
/// </summary>
[Collection("api")]
public sealed class ConsentimentoTests : IAsyncLifetime
{
    private const string Senha = "senha-segura-123";

    private readonly HttpClient _cliente;
    private readonly IServiceScope _escopo;
    private readonly FisgaDbContext _db;
    private readonly ICadastroDeUsuario _cadastro;
    private readonly UserManager<ApplicationUser> _usuarios;
    private readonly LegalOptions _opcoesLegais;

    public ConsentimentoTests(ApiTestFixture fixture)
    {
        _cliente = fixture.Factory.CreateClient();
        _escopo = fixture.Factory.Services.CreateScope();
        _db = _escopo.ServiceProvider.GetRequiredService<FisgaDbContext>();
        _cadastro = _escopo.ServiceProvider.GetRequiredService<ICadastroDeUsuario>();
        _usuarios = _escopo.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        _opcoesLegais = _escopo.ServiceProvider.GetRequiredService<LegalOptions>();
    }

    public async Task InitializeAsync()
    {
        // Ordem explícita: consentimentos, sessões e tokens apontam para usuários.
        await _db.Consentimentos.ExecuteDeleteAsync();
        await _db.Sessoes.ExecuteDeleteAsync();
        await _db.TokensDeLogin.ExecuteDeleteAsync();
        await _db.Usuarios.ExecuteDeleteAsync();
        await _db.Users.ExecuteDeleteAsync();
        EmailSenderFake.Limpar();
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _escopo.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Login_sem_aceite_da_versao_atual_recebe_403_em_rota_de_dados()
    {
        var usuario = await CriarUsuarioConfirmadoAsync();
        await EntrarAsync(usuario.Email.Valor);

        var resposta = await _cliente.GetAsync("/api/diario");

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        var erro = await resposta.Content.ReadFromJsonAsync<ErroResponse>();
        Assert.NotNull(erro);
        Assert.Equal("consentimento_pendente", erro!.Codigo);
    }

    [Fact]
    public async Task Me_continua_200_e_sinaliza_consentimento_pendente()
    {
        var usuario = await CriarUsuarioConfirmadoAsync();
        await EntrarAsync(usuario.Email.Valor);

        var me = await _cliente.GetFromJsonAsync<MeResponse>("/api/auth/me");

        Assert.NotNull(me);
        Assert.True(me!.Autenticado);
        Assert.Equal(usuario.Id, me.Usuario!.Id);
        Assert.True(me.ConsentimentoPendente);
    }

    [Fact]
    public async Task Aceitar_versoes_vigentes_grava_os_dois_tipos_e_libera_o_app()
    {
        var usuario = await CriarUsuarioConfirmadoAsync();
        await EntrarAsync(usuario.Email.Valor);

        var resposta = await _cliente.PostAsJsonAsync("/api/conta/consentimento", new
        {
            termosVersao = _opcoesLegais.TermsVersion,
            privacidadeVersao = _opcoesLegais.PrivacyVersion
        });
        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);

        // Aceitar a mesma versão duas vezes não duplica linha (índice único).
        var repetido = await _cliente.PostAsJsonAsync("/api/conta/consentimento", new
        {
            termosVersao = _opcoesLegais.TermsVersion,
            privacidadeVersao = _opcoesLegais.PrivacyVersion
        });
        Assert.Equal(HttpStatusCode.NoContent, repetido.StatusCode);

        var consentimentos = await _db.Consentimentos
            .Where(c => c.UsuarioId == usuario.Id)
            .ToListAsync();
        Assert.Equal(2, consentimentos.Count);
        Assert.Contains(consentimentos, c =>
            c.Tipo == TipoDeConsentimento.Termos && c.Versao == _opcoesLegais.TermsVersion);
        Assert.Contains(consentimentos, c =>
            c.Tipo == TipoDeConsentimento.Privacidade && c.Versao == _opcoesLegais.PrivacyVersion);

        Assert.Equal(HttpStatusCode.OK, (await _cliente.GetAsync("/api/diario")).StatusCode);

        var me = await _cliente.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.NotNull(me);
        Assert.False(me!.ConsentimentoPendente);
    }

    [Fact]
    public async Task Aceitar_com_versao_errada_retorna_400_e_nao_grava_nada()
    {
        var usuario = await CriarUsuarioConfirmadoAsync();
        await EntrarAsync(usuario.Email.Valor);

        var resposta = await _cliente.PostAsJsonAsync("/api/conta/consentimento", new
        {
            termosVersao = "versao-errada",
            privacidadeVersao = _opcoesLegais.PrivacyVersion
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Empty(await _db.Consentimentos.Where(c => c.UsuarioId == usuario.Id).ToListAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.GetAsync("/api/diario")).StatusCode);
    }

    [Fact]
    public async Task Nova_versao_vigente_volta_a_bloquear_usuario_que_aceitou_a_versao_anterior()
    {
        var usuario = await CriarUsuarioConfirmadoAsync();
        await EntrarAsync(usuario.Email.Valor);

        var aceite = await _cliente.PostAsJsonAsync("/api/conta/consentimento", new
        {
            termosVersao = _opcoesLegais.TermsVersion,
            privacidadeVersao = _opcoesLegais.PrivacyVersion
        });
        Assert.Equal(HttpStatusCode.NoContent, aceite.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _cliente.GetAsync("/api/diario")).StatusCode);

        var versaoAnterior = _opcoesLegais.TermsVersion;
        try
        {
            _opcoesLegais.TermsVersion = "2027-01-01";

            var bloqueado = await _cliente.GetAsync("/api/diario");
            Assert.Equal(HttpStatusCode.Forbidden, bloqueado.StatusCode);
            var erro = await bloqueado.Content.ReadFromJsonAsync<ErroResponse>();
            Assert.Equal("consentimento_pendente", erro!.Codigo);

            var me = await _cliente.GetFromJsonAsync<MeResponse>("/api/auth/me");
            Assert.True(me!.ConsentimentoPendente);
        }
        finally
        {
            _opcoesLegais.TermsVersion = versaoAnterior;
        }
    }

    [Fact]
    public async Task Usuario_nao_autenticado_nao_e_afetado_pelo_middleware()
    {
        var protegido = await _cliente.GetAsync("/api/diario");
        Assert.Equal(HttpStatusCode.Unauthorized, protegido.StatusCode);

        var me = await _cliente.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.NotNull(me);
        Assert.False(me!.Autenticado);
        Assert.False(me.ConsentimentoPendente);
    }

    private async Task<Usuario> CriarUsuarioConfirmadoAsync()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var usuario = await _cadastro.CriarComSenhaAsync(email, "Pescador", Senha);
        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        conta!.EmailConfirmed = true;
        await _usuarios.UpdateAsync(conta);
        return usuario;
    }

    private async Task EntrarAsync(string email)
    {
        var resposta = await _cliente.PostAsJsonAsync("/api/auth/entrar", new { email, senha = Senha });
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<EntrarResponse>();
        Assert.True(corpo!.Autenticado);
    }
}

using Fisga.Application.Identidade;
using Fisga.Domain.Common;
using Fisga.Domain.Identidade.Eventos;

namespace Fisga.Application.Tests;

public sealed class ServicoDeAutenticacaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 16, 6, 30, 0, TimeSpan.FromHours(-3));

    private sealed record Cenario(
        ServicoDeAutenticacao Servico,
        UsuarioRepositorioFake Usuarios,
        SessaoRepositorioFake Sessoes,
        TokenDeLoginRepositorioFake Tokens,
        EmailSenderFake Email,
        UnitOfWorkFake Unidade,
        RelogioFixo Relogio,
        DispatcherFake Eventos);

    private static Cenario Criar()
    {
        var usuarios = new UsuarioRepositorioFake();
        var sessoes = new SessaoRepositorioFake();
        var tokens = new TokenDeLoginRepositorioFake();
        var email = new EmailSenderFake();
        var unidade = new UnitOfWorkFake();
        var relogio = new RelogioFixo(Agora);
        var eventos = new DispatcherFake();
        var opcoes = new AutenticacaoOptions
        {
            ValidadeDoLinkEmMinutos = 15,
            DuracaoDaSessaoEmDias = 30,
            IntervaloDeReenvioEmSegundos = 60,
            LinkBaseUrl = "https://api.fisga.test",
            WebUrl = "https://fisga.test"
        };

        var servico = new ServicoDeAutenticacao(
            usuarios,
            sessoes,
            tokens,
            email,
            unidade,
            relogio,
            eventos,
            opcoes);

        return new Cenario(servico, usuarios, sessoes, tokens, email, unidade, relogio, eventos);
    }

    private static string TokenDoEmail(EmailSenderFake email)
    {
        var url = email.Enviados[^1].Url;
        return Uri.UnescapeDataString(url[(url.IndexOf("token=", StringComparison.Ordinal) + 6)..]);
    }

    [Fact]
    public async Task Solicitar_link_normaliza_email_e_envia_url()
    {
        var cenario = Criar();

        var resultado = await cenario.Servico.SolicitarLinkAsync("  Abson.Dev@Exemplo.COM ");

        var token = Assert.Single(cenario.Tokens.Tokens);
        Assert.Equal("abson.dev@exemplo.com", token.Email);
        Assert.Single(cenario.Email.Enviados);
        Assert.Equal("abson.dev@exemplo.com", cenario.Email.Enviados[0].Destino);
        Assert.Contains("/api/auth/confirmar?token=", cenario.Email.Enviados[0].Url);
        Assert.Equal(cenario.Email.Enviados[0].Url, resultado.Link);
        Assert.Empty(cenario.Usuarios.Usuarios);
    }

    [Fact]
    public async Task Nao_reenvia_dentro_do_intervalo()
    {
        var cenario = Criar();

        await cenario.Servico.SolicitarLinkAsync("abson@exemplo.com");
        var segundo = await cenario.Servico.SolicitarLinkAsync("abson@exemplo.com");

        Assert.Single(cenario.Tokens.Tokens);
        Assert.Single(cenario.Email.Enviados);
        Assert.Equal(string.Empty, segundo.Link);
    }

    [Fact]
    public async Task Reenvio_apos_intervalo_invalida_link_anterior()
    {
        var cenario = Criar();

        await cenario.Servico.SolicitarLinkAsync("abson@exemplo.com");
        var primeiro = cenario.Tokens.Tokens[0];
        cenario.Relogio.Avancar(TimeSpan.FromSeconds(61));

        await cenario.Servico.SolicitarLinkAsync("abson@exemplo.com");

        Assert.Equal(2, cenario.Tokens.Tokens.Count);
        Assert.NotNull(primeiro.UsadoEm);
        Assert.Equal(2, cenario.Email.Enviados.Count);
    }

    [Fact]
    public async Task Confirmar_cria_usuario_sessao_e_publica_eventos()
    {
        var cenario = Criar();
        await cenario.Servico.SolicitarLinkAsync("abson@exemplo.com");
        var token = TokenDoEmail(cenario.Email);

        var sessao = await cenario.Servico.ConfirmarAsync(token, "Playwright", "127.0.0.1");

        var usuario = Assert.Single(cenario.Usuarios.Usuarios);
        Assert.NotNull(usuario.EmailVerificadoEm);
        Assert.Single(cenario.Sessoes.Sessoes);
        Assert.Equal(usuario.Id, cenario.Sessoes.Sessoes[0].UsuarioId);
        Assert.Equal(32, sessao.Token.Length);
        Assert.True(sessao.ExpiraEm > cenario.Relogio.Agora);
        Assert.True(sessao.PrimeiroAcesso);
        Assert.Contains(cenario.Eventos.Publicados, evento => evento is UsuarioCriado);
        Assert.Contains(cenario.Eventos.Publicados, evento => evento is SessaoIniciada);
    }

    [Fact]
    public async Task Link_nao_pode_ser_usado_duas_vezes()
    {
        var cenario = Criar();
        await cenario.Servico.SolicitarLinkAsync("abson@exemplo.com");
        var token = TokenDoEmail(cenario.Email);
        await cenario.Servico.ConfirmarAsync(token, null, null);

        var excecao = await Assert.ThrowsAsync<DomainException>(() =>
            cenario.Servico.ConfirmarAsync(token, null, null));

        Assert.Equal("link_ja_usado", excecao.Codigo);
        Assert.Single(cenario.Usuarios.Usuarios);
        Assert.Single(cenario.Sessoes.Sessoes);
    }

    [Fact]
    public async Task Link_expirado_nao_cria_sessao()
    {
        var cenario = Criar();
        await cenario.Servico.SolicitarLinkAsync("abson@exemplo.com");
        var token = TokenDoEmail(cenario.Email);
        cenario.Relogio.Avancar(TimeSpan.FromMinutes(16));

        var excecao = await Assert.ThrowsAsync<DomainException>(() =>
            cenario.Servico.ConfirmarAsync(token, null, null));

        Assert.Equal("link_expirado", excecao.Codigo);
        Assert.Empty(cenario.Usuarios.Usuarios);
        Assert.Empty(cenario.Sessoes.Sessoes);
    }

    [Fact]
    public async Task Valida_sessao_e_logout_revoga()
    {
        var cenario = Criar();
        await cenario.Servico.SolicitarLinkAsync("abson@exemplo.com");
        var criada = await cenario.Servico.ConfirmarAsync(TokenDoEmail(cenario.Email), null, null);

        var valida = await cenario.Servico.ValidarSessaoAsync(criada.Token);
        Assert.NotNull(valida);
        Assert.Equal("abson@exemplo.com", valida.Usuario.Email.Valor);

        await cenario.Servico.SairAsync(criada.Token);

        Assert.Null(await cenario.Servico.ValidarSessaoAsync(criada.Token));
        Assert.NotNull(cenario.Sessoes.Sessoes[0].RevogadaEm);
    }

    [Fact]
    public async Task Sessao_expira_em_30_dias()
    {
        var cenario = Criar();
        await cenario.Servico.SolicitarLinkAsync("abson@exemplo.com");
        var criada = await cenario.Servico.ConfirmarAsync(TokenDoEmail(cenario.Email), null, null);
        cenario.Relogio.Avancar(TimeSpan.FromDays(31));

        Assert.Null(await cenario.Servico.ValidarSessaoAsync(criada.Token));
    }

    [Fact]
    public async Task Segundo_login_reutiliza_o_mesmo_usuario()
    {
        var cenario = Criar();
        await cenario.Servico.SolicitarLinkAsync("abson@exemplo.com");
        await cenario.Servico.ConfirmarAsync(TokenDoEmail(cenario.Email), null, null);

        cenario.Relogio.Avancar(TimeSpan.FromSeconds(61));
        await cenario.Servico.SolicitarLinkAsync("abson@exemplo.com");
        var segundo = await cenario.Servico.ConfirmarAsync(TokenDoEmail(cenario.Email), null, null);

        Assert.Single(cenario.Usuarios.Usuarios);
        Assert.Equal(2, cenario.Sessoes.Sessoes.Count);
        Assert.False(segundo.PrimeiroAcesso);
    }

    [Fact]
    public async Task Email_invalido_e_rejeitado()
    {
        var cenario = Criar();

        var excecao = await Assert.ThrowsAsync<DomainException>(() =>
            cenario.Servico.SolicitarLinkAsync("nao-e-email"));

        Assert.Equal("email_invalido", excecao.Codigo);
        Assert.Empty(cenario.Tokens.Tokens);
    }
}

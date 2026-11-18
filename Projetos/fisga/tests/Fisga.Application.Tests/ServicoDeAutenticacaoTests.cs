using Fisga.Application.Abstractions;
using Fisga.Application.Identidade;
using Fisga.Domain.Common;
using Fisga.Domain.Identidade;
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
        var cadastro = new CadastroDeUsuarioFake(usuarios, relogio, eventos);
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
            cadastro,
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
    public async Task Confirmar_cria_usuario_sem_sessao_e_publica_eventos()
    {
        var cenario = Criar();
        await cenario.Servico.SolicitarLinkAsync("abson@exemplo.com");
        var token = TokenDoEmail(cenario.Email);

        var resultado = await cenario.Servico.ConfirmarAsync(token);

        var usuario = Assert.Single(cenario.Usuarios.Usuarios);
        Assert.Equal(usuario, resultado.Usuario);
        Assert.NotNull(usuario.EmailVerificadoEm);
        Assert.True(resultado.PrimeiroAcesso);

        // O serviço de token não cria sessão nem emissor de cookie: a sessão
        // é criada pelo endpoint via IdentitySessionService.AssinarAsync.
        Assert.Empty(cenario.Sessoes.Sessoes);

        Assert.Contains(cenario.Eventos.Publicados, evento => evento is UsuarioCriado);
        Assert.DoesNotContain(cenario.Eventos.Publicados, evento => evento is SessaoIniciada);
    }

    [Fact]
    public async Task Link_nao_pode_ser_usado_duas_vezes()
    {
        var cenario = Criar();
        await cenario.Servico.SolicitarLinkAsync("abson@exemplo.com");
        var token = TokenDoEmail(cenario.Email);
        await cenario.Servico.ConfirmarAsync(token);

        var excecao = await Assert.ThrowsAsync<DomainException>(() =>
            cenario.Servico.ConfirmarAsync(token));

        Assert.Equal("link_ja_usado", excecao.Codigo);
        Assert.Single(cenario.Usuarios.Usuarios);
        Assert.Empty(cenario.Sessoes.Sessoes);
    }

    [Fact]
    public async Task Link_expirado_nao_cria_usuario_nem_sessao()
    {
        var cenario = Criar();
        await cenario.Servico.SolicitarLinkAsync("abson@exemplo.com");
        var token = TokenDoEmail(cenario.Email);
        cenario.Relogio.Avancar(TimeSpan.FromMinutes(16));

        var excecao = await Assert.ThrowsAsync<DomainException>(() =>
            cenario.Servico.ConfirmarAsync(token));

        Assert.Equal("link_expirado", excecao.Codigo);
        Assert.Empty(cenario.Usuarios.Usuarios);
        Assert.Empty(cenario.Sessoes.Sessoes);
    }

    [Fact]
    public async Task Segundo_login_reutiliza_o_mesmo_usuario()
    {
        var cenario = Criar();
        await cenario.Servico.SolicitarLinkAsync("abson@exemplo.com");
        await cenario.Servico.ConfirmarAsync(TokenDoEmail(cenario.Email));

        cenario.Relogio.Avancar(TimeSpan.FromSeconds(61));
        await cenario.Servico.SolicitarLinkAsync("abson@exemplo.com");
        var segundo = await cenario.Servico.ConfirmarAsync(TokenDoEmail(cenario.Email));

        Assert.Single(cenario.Usuarios.Usuarios);
        Assert.Empty(cenario.Sessoes.Sessoes);
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

    /// <summary>
    /// Fake de ICadastroDeUsuario que reproduz o contrato novo: garante o usuário
    /// de domínio (e, na implementação real, a ApplicationUser) publicando o
    /// evento de criação — exatamente o que o serviço de token passa a consumir.
    /// </summary>
    private sealed class CadastroDeUsuarioFake(
        IUsuarioRepository usuarios,
        IRelogio relogio,
        IDomainEventDispatcher eventos) : ICadastroDeUsuario
    {
        public async Task<Usuario> GarantirUsuarioAsync(
            Email email,
            string? nome,
            bool emailVerificado,
            CancellationToken ct = default)
        {
            var existente = await usuarios.ObterPorEmailAsync(email, ct);
            if (existente is not null)
            {
                return existente;
            }

            var usuario = Usuario.Criar(email, nome, relogio.Agora);
            if (emailVerificado)
            {
                usuario.VerificarEmail(relogio.Agora);
            }

            usuarios.Adicionar(usuario);
            await eventos.PublicarAsync(usuario.Eventos.ToList(), ct);
            usuario.LimparEventos();
            return usuario;
        }

        public Task<Usuario> CriarComSenhaAsync(
            string email,
            string? nome,
            string senha,
            CancellationToken ct = default) =>
            GarantirUsuarioAsync(Email.Criar(email), nome, emailVerificado: false, ct);

        public Task<Usuario?> ObterUsuarioAsync(Guid id, CancellationToken ct = default) =>
            usuarios.ObterPorIdAsync(id, ct);
    }
}

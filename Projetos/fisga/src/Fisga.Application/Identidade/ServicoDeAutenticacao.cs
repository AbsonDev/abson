using Fisga.Application.Abstractions;
using Fisga.Application.Seguranca;
using Fisga.Domain.Common;
using Fisga.Domain.Identidade;
using Fisga.Domain.Identidade.Eventos;

namespace Fisga.Application.Identidade;

public sealed class ServicoDeAutenticacao(
    IUsuarioRepository usuarios,
    ISessaoRepository sessoes,
    ITokenDeLoginRepository tokens,
    IEmailSender email,
    IUnitOfWork unidade,
    IRelogio relogio,
    IDomainEventDispatcher eventos,
    AutenticacaoOptions opcoes)
{
    public async Task<ResultadoDaSolicitacao> SolicitarLinkAsync(
        string? emailEntrada,
        CancellationToken ct = default)
    {
        var emailNormalizado = Email.Criar(emailEntrada);
        var agora = relogio.Agora;

        var ultimo = await tokens.ObterUltimoPorEmailAsync(emailNormalizado.Valor, ct);
        if (ultimo is not null
            && ultimo.CriadoEm > agora.AddSeconds(-opcoes.IntervaloDeReenvioEmSegundos))
        {
            // Resposta genérica: não revela se o e-mail existe nem se houve reenvio.
            return new ResultadoDaSolicitacao(emailNormalizado.Valor, string.Empty);
        }

        await tokens.InvalidarPendentesAsync(emailNormalizado.Valor, agora, ct);

        var tokenCru = TokensSeguros.Novo();
        var registro = TokenDeLogin.Criar(
            emailNormalizado.Valor,
            TokensSeguros.Hash(tokenCru),
            agora,
            TimeSpan.FromMinutes(opcoes.ValidadeDoLinkEmMinutos));

        tokens.Adicionar(registro);
        await unidade.SalvarAsync(ct);

        var url = $"{opcoes.LinkBaseUrl.TrimEnd('/')}/api/auth/confirmar?token={Uri.EscapeDataString(tokenCru)}";

        await email.EnviarLinkDeAcessoAsync(
            emailNormalizado.Valor,
            null,
            url,
            registro.ExpiraEm,
            ct);

        return new ResultadoDaSolicitacao(emailNormalizado.Valor, url);
    }

    public async Task<SessaoCriada> ConfirmarAsync(
        string? tokenCru,
        string? userAgent,
        string? ip,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tokenCru) || tokenCru.Trim().Length < 32)
        {
            throw new DomainException("link_invalido", "Este link de acesso é inválido. Peça um novo.");
        }

        var hash = TokensSeguros.Hash(tokenCru.Trim());
        var registro = await tokens.ObterPorHashAsync(hash, ct)
                       ?? throw new DomainException("link_invalido", "Este link de acesso é inválido. Peça um novo.");

        var agora = relogio.Agora;
        registro.Consumir(agora);

        var emailNormalizado = Email.Criar(registro.Email);
        var usuario = await usuarios.ObterPorEmailAsync(emailNormalizado, ct);

        if (usuario is null)
        {
            usuario = Usuario.Criar(emailNormalizado, null, agora);
            usuarios.Adicionar(usuario);
        }

        var primeiroAcesso = usuario.BemVindoEm is null;
        usuario.VerificarEmail(agora);
        usuario.RegistrarAcesso(agora);
        usuario.MarcarBoasVindas(agora);

        var tokenSessao = TokensSeguros.Novo();
        var sessao = Sessao.Criar(
            usuario.Id,
            TokensSeguros.Hash(tokenSessao),
            userAgent,
            ip is null ? null : TokensSeguros.Hash(ip),
            agora,
            TimeSpan.FromDays(opcoes.DuracaoDaSessaoEmDias));

        sessoes.Adicionar(sessao);
        await unidade.SalvarAsync(ct);

        var pendentes = usuario.Eventos
            .Concat([new SessaoIniciada(usuario.Id, sessao.Id, agora)])
            .ToList();

        await eventos.PublicarAsync(pendentes, ct);
        usuario.LimparEventos();

        return new SessaoCriada(tokenSessao, sessao.ExpiraEm, usuario, primeiroAcesso);
    }

    public async Task<SessaoValida?> ValidarSessaoAsync(string? tokenCru, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tokenCru))
        {
            return null;
        }

        var hash = TokensSeguros.Hash(tokenCru.Trim());
        var sessao = await sessoes.ObterPorHashAsync(hash, ct);
        var agora = relogio.Agora;

        if (sessao is null || !sessao.EstaAtiva(agora))
        {
            return null;
        }

        var usuario = await usuarios.ObterPorIdAsync(sessao.UsuarioId, ct);
        if (usuario is null)
        {
            return null;
        }

        // Evita um UPDATE a cada requisição; registra o uso em janelas de 5 minutos.
        if (agora - sessao.UltimoUsoEm > TimeSpan.FromMinutes(5))
        {
            sessao.RegistrarUso(agora);
            await unidade.SalvarAsync(ct);
        }

        return new SessaoValida(sessao, usuario);
    }

    public async Task SairAsync(string? tokenCru, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tokenCru))
        {
            return;
        }

        var hash = TokensSeguros.Hash(tokenCru.Trim());
        var sessao = await sessoes.ObterPorHashAsync(hash, ct);
        if (sessao is null)
        {
            return;
        }

        sessao.Revogar(relogio.Agora);
        await unidade.SalvarAsync(ct);
    }

    public async Task SairDeTodosAsync(Guid usuarioId, CancellationToken ct = default)
    {
        await sessoes.RevogarTodasAsync(usuarioId, relogio.Agora, exceto: null, ct);
        await unidade.SalvarAsync(ct);
    }

    public async Task<Usuario> AtualizarPerfilAsync(
        Guid usuarioId,
        string? nome,
        bool aceitaNovidades,
        CancellationToken ct = default)
    {
        var usuario = await usuarios.ObterPorIdAsync(usuarioId, ct)
                       ?? throw new RecursoNaoEncontradoException("usuario_nao_encontrado", "Conta não encontrada.");

        usuario.AtualizarPerfil(nome, aceitaNovidades, relogio.Agora);
        await unidade.SalvarAsync(ct);
        return usuario;
    }

    public async Task<IReadOnlyList<Sessao>> ListarSessoesAsync(
        Guid usuarioId,
        CancellationToken ct = default)
    {
        return await sessoes.ListarAtivasAsync(usuarioId, relogio.Agora, ct);
    }
}

using Fisga.Application.Abstractions;
using Fisga.Application.Seguranca;
using Fisga.Domain.Common;
using Fisga.Domain.Identidade;

namespace Fisga.Application.Identidade;

public sealed class ServicoDeAutenticacao(
    IUsuarioRepository usuarios,
    ISessaoRepository sessoes,
    ITokenDeLoginRepository tokens,
    IEmailSender email,
    IUnitOfWork unidade,
    IRelogio relogio,
    ICadastroDeUsuario cadastro,
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

    /// <summary>
    /// Valida o link mágico, garante o usuário de domínio (e a conta da Identity,
    /// via <see cref="ICadastroDeUsuario"/>) e registra o acesso. Não cria sessão,
    /// token de cookie nem publica eventos de sessão: isso é feito pelo endpoint
    /// através de IdentitySessionService.AssinarAsync.
    /// </summary>
    public async Task<ResultadoDaConfirmacao> ConfirmarAsync(
        string? tokenCru,
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
        var usuario = await cadastro.GarantirUsuarioAsync(
            emailNormalizado,
            nome: null,
            emailVerificado: true,
            ct);

        var primeiroAcesso = usuario.BemVindoEm is null;
        usuario.VerificarEmail(agora);
        usuario.RegistrarAcesso(agora);
        usuario.MarcarBoasVindas(agora);

        await unidade.SalvarAsync(ct);

        return new ResultadoDaConfirmacao(usuario, primeiroAcesso);
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

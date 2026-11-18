using Fisga.Domain.Identidade;

namespace Fisga.Application.Identidade;

public sealed class AutenticacaoOptions
{
    public int ValidadeDoLinkEmMinutos { get; set; } = 15;

    public int DuracaoDaSessaoEmDias { get; set; } = 30;

    public int IntervaloDeReenvioEmSegundos { get; set; } = 60;

    public string LinkBaseUrl { get; set; } = "http://localhost:5184";

    public string WebUrl { get; set; } = "http://localhost:4323";

    /// <summary>
    /// Nome do cookie legado <c>fisga_sessao</c>, mantido apenas para apagá-lo no
    /// logout/exclusão. A sessão atual usa o cookie <c>fisga_auth</c> da Identity.
    /// </summary>
    public string LegacyCookieName { get; set; } = "fisga_sessao";

    public bool CookieSecure { get; set; } = true;
}

public sealed class EmailOptions
{
    public string Provider { get; set; } = "Console";

    public string From { get; set; } = "Fisga <acesso@abson.tech>";

    public string? ResendApiKey { get; set; }

    public string? ReplyTo { get; set; }

    /// <summary>
    /// Diretório do outbox de teste (<c>Email:Provider=Outbox</c>). Vazio em
    /// produção; nunca aponte para um diretório versionado.
    /// </summary>
    public string? OutboxPath { get; set; }
}

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorEmailAsync(Email email, CancellationToken ct = default);

    Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Usuario>> ListarAceitaNovidadesAsync(CancellationToken ct = default);

    void Adicionar(Usuario usuario);

    void Remover(Usuario usuario);
}

public interface ISessaoRepository
{
    Task<Sessao?> ObterPorIdAsync(Guid id, CancellationToken ct = default);

    Task<Sessao?> ObterPorHashAsync(string tokenHash, CancellationToken ct = default);

    Task<IReadOnlyList<Sessao>> ListarAtivasAsync(Guid usuarioId, DateTimeOffset agora, CancellationToken ct = default);

    void Adicionar(Sessao sessao);

    Task RevogarTodasAsync(Guid usuarioId, DateTimeOffset agora, Guid? exceto, CancellationToken ct = default);
}

public interface ITokenDeLoginRepository
{
    Task<TokenDeLogin?> ObterPorHashAsync(string tokenHash, CancellationToken ct = default);

    Task<TokenDeLogin?> ObterUltimoPorEmailAsync(string email, CancellationToken ct = default);

    Task InvalidarPendentesAsync(string email, DateTimeOffset agora, CancellationToken ct = default);

    void Adicionar(TokenDeLogin token);
}

public interface IUnitOfWork
{
    Task<int> SalvarAsync(CancellationToken ct = default);
}

public interface IEmailSender
{
    Task EnviarLinkDeAcessoAsync(
        string destino,
        string? nome,
        string url,
        DateTimeOffset expiraEm,
        CancellationToken ct = default);

    Task EnviarAsync(
        string destino,
        string assunto,
        string html,
        string texto,
        string? listUnsubscribeUrl = null,
        CancellationToken ct = default);
}

public sealed record ResultadoDaSolicitacao(string Email, string Link);

/// <summary>
/// Resultado da confirmação do link mágico. O serviço apenas garante o usuário e
/// atualiza o acesso; a sessão de dispositivo e o cookie são criados pelo endpoint
/// via IdentitySessionService.AssinarAsync.
/// </summary>
public sealed record ResultadoDaConfirmacao(Usuario Usuario, bool PrimeiroAcesso);

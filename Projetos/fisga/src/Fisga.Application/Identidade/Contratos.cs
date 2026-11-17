using Fisga.Domain.Identidade;

namespace Fisga.Application.Identidade;

public sealed class AutenticacaoOptions
{
    public int ValidadeDoLinkEmMinutos { get; set; } = 15;

    public int DuracaoDaSessaoEmDias { get; set; } = 30;

    public int IntervaloDeReenvioEmSegundos { get; set; } = 60;

    public string LinkBaseUrl { get; set; } = "http://localhost:5184";

    public string WebUrl { get; set; } = "http://localhost:4323";

    public string CookieName { get; set; } = "fisga_sessao";

    public bool CookieSecure { get; set; } = true;
}

public sealed class EmailOptions
{
    public string Provider { get; set; } = "Console";

    public string From { get; set; } = "Fisga <acesso@abson.tech>";

    public string? ResendApiKey { get; set; }

    public string? ReplyTo { get; set; }
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

public sealed record ResultadoDoRegistro(Usuario Usuario, string LinkDeConfirmacao);

public sealed record SessaoValida(Sessao Sessao, Usuario Usuario);

public sealed record SessaoCriada(string Token, DateTimeOffset ExpiraEm, Usuario Usuario, bool PrimeiroAcesso);

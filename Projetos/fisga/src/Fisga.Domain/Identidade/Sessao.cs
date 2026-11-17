using Fisga.Domain.Common;

namespace Fisga.Domain.Identidade;

/// <summary>
/// Sessão de login. O banco guarda apenas o hash do token; o token cru vive
/// no cookie HttpOnly do navegador.
/// </summary>
public sealed class Sessao
{
    private Sessao()
    {
    }

    private Sessao(
        Guid id,
        Guid usuarioId,
        string tokenHash,
        string? userAgent,
        string? ipHash,
        DateTimeOffset criadoEm,
        DateTimeOffset expiraEm)
    {
        Id = id;
        UsuarioId = usuarioId;
        TokenHash = tokenHash;
        UserAgent = userAgent;
        IpHash = ipHash;
        CriadoEm = criadoEm;
        UltimoUsoEm = criadoEm;
        ExpiraEm = expiraEm;
    }

    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public string? UserAgent { get; private set; }

    public string? IpHash { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset UltimoUsoEm { get; private set; }

    public DateTimeOffset ExpiraEm { get; private set; }

    public DateTimeOffset? RevogadaEm { get; private set; }

    public bool EstaAtiva(DateTimeOffset agora) => RevogadaEm is null && ExpiraEm > agora;

    public static Sessao Criar(
        Guid usuarioId,
        string tokenHash,
        string? userAgent,
        string? ipHash,
        DateTimeOffset agora,
        TimeSpan duracao)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new DomainException("sessao_sem_usuario", "Sessão sem usuário.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length != 64)
        {
            throw new DomainException("sessao_sem_token", "Sessão sem token válido.");
        }

        if (duracao <= TimeSpan.Zero || duracao > TimeSpan.FromDays(90))
        {
            throw new DomainException("sessao_duracao_invalida", "Duração de sessão inválida.");
        }

        return new Sessao(
            Guid.NewGuid(),
            usuarioId,
            tokenHash,
            Limitar(userAgent, 300),
            Limitar(ipHash, 64),
            agora,
            agora.Add(duracao));
    }

    public void RegistrarUso(DateTimeOffset agora)
    {
        if (agora > UltimoUsoEm)
        {
            UltimoUsoEm = agora;
        }
    }

    public void Revogar(DateTimeOffset agora)
    {
        RevogadaEm ??= agora;
    }

    private static string? Limitar(string? valor, int maximo) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim()[..Math.Min(valor.Trim().Length, maximo)];
}

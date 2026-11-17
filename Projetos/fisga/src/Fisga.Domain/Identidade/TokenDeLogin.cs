using Fisga.Domain.Common;

namespace Fisga.Domain.Identidade;

/// <summary>
/// Link mágico de uso único. Guardamos o hash e o e-mail; o usuário só é
/// criado quando o link é confirmado.
/// </summary>
public sealed class TokenDeLogin
{
    private TokenDeLogin()
    {
    }

    private TokenDeLogin(
        Guid id,
        string email,
        string tokenHash,
        DateTimeOffset criadoEm,
        DateTimeOffset expiraEm)
    {
        Id = id;
        Email = email;
        TokenHash = tokenHash;
        CriadoEm = criadoEm;
        ExpiraEm = expiraEm;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset ExpiraEm { get; private set; }

    public DateTimeOffset? UsadoEm { get; private set; }

    public static TokenDeLogin Criar(
        string email,
        string tokenHash,
        DateTimeOffset agora,
        TimeSpan validade)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254)
        {
            throw new DomainException("token_email_invalido", "Token de login sem e-mail válido.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length != 64)
        {
            throw new DomainException("token_login_invalido", "Token de login inválido.");
        }

        if (validade <= TimeSpan.Zero || validade > TimeSpan.FromHours(1))
        {
            throw new DomainException("token_validade_invalida", "Validade do link inválida.");
        }

        return new TokenDeLogin(
            Guid.NewGuid(),
            email.Trim().ToLowerInvariant(),
            tokenHash,
            agora,
            agora.Add(validade));
    }

    public bool EstaValido(DateTimeOffset agora) => UsadoEm is null && ExpiraEm > agora;

    public void Consumir(DateTimeOffset agora)
    {
        if (UsadoEm is not null)
        {
            throw new DomainException("link_ja_usado", "Este link já foi usado. Peça um novo.");
        }

        if (ExpiraEm <= agora)
        {
            throw new DomainException("link_expirado", "Este link expirou. Peça um novo.");
        }

        UsadoEm = agora;
    }

    public void Invalidar(DateTimeOffset agora)
    {
        UsadoEm ??= agora;
    }
}

namespace Fisga.Api;

/// <summary>
/// Valida o nome do cookie legado no start. <c>LimparCookie</c> roda em logins
/// bem-sucedidos; apontar para <c>fisga_auth</c> apagaria a sessão recém-emitida.
/// </summary>
public static class ConfiguracaoDeAutenticacao
{
    private const string CookieDaIdentity = "fisga_auth";

    public static void Validar(string? legacyCookieName)
    {
        if (string.IsNullOrWhiteSpace(legacyCookieName))
        {
            throw new InvalidOperationException(
                "Auth:LegacyCookieName não pode ser vazio: ele é apagado no login/logout. " +
                "Use fisga_sessao (nome do cookie legado).");
        }

        if (string.Equals(legacyCookieName, CookieDaIdentity, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Auth:LegacyCookieName não pode ser fisga_auth: o login apagaria a sessão recém-emitida. " +
                "Use fisga_sessao (nome do cookie legado).");
        }
    }
}

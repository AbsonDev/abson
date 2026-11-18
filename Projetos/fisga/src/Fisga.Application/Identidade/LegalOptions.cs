namespace Fisga.Application.Identidade;

/// <summary>
/// Versões vigentes dos termos de uso e da política de privacidade, lidas da
/// configuração (<c>Legal:TermsVersion</c> e <c>Legal:PrivacyVersion</c>). O
/// aceite do usuário é que é lido do banco a cada requisição autenticada.
/// </summary>
public sealed class LegalOptions
{
    public string TermsVersion { get; set; } = "2026-11-17";

    public string PrivacyVersion { get; set; } = "2026-11-17";
}

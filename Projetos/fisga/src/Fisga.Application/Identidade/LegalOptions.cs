namespace Fisga.Application.Identidade;

/// <summary>
/// Versões vigentes dos termos de uso e da política de privacidade. Ligado a
/// <c>Legal:TermsVersion</c> e <c>Legal:PrivacyVersion</c>; o aceite é sempre
/// avaliado contra a versão atual, lida do banco a cada requisição.
/// </summary>
public sealed class LegalOptions
{
    public string TermsVersion { get; set; } = "2026-11-17";

    public string PrivacyVersion { get; set; } = "2026-11-17";
}

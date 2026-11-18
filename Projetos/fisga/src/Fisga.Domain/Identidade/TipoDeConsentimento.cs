namespace Fisga.Domain.Identidade;

/// <summary>
/// Tipo de termo que o usuário aceita explicitamente. O aceite é exigido para a
/// versão vigente de cada tipo antes de liberar o app.
/// </summary>
public enum TipoDeConsentimento
{
    Termos = 1,
    Privacidade = 2
}

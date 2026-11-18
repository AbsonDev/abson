using Fisga.Domain.Identidade;

namespace Fisga.Application.Identidade;

public interface IConsentimentoRepository
{
    Task<Consentimento?> ObterUltimoAsync(
        Guid usuarioId,
        TipoDeConsentimento tipo,
        CancellationToken ct = default);

    Task<bool> TemVersaoAsync(
        Guid usuarioId,
        TipoDeConsentimento tipo,
        string versao,
        CancellationToken ct = default);

    void Adicionar(Consentimento consentimento);
}

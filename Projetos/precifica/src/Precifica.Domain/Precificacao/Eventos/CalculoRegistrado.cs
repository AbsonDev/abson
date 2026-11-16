using Precifica.Domain.Common;

namespace Precifica.Domain.Precificacao.Eventos;

/// <summary>
/// Registrado quando um cálculo é salvo. Serve para analytics por nicho e para
/// futuras automações (ex.: e-mail com o PDF).
/// </summary>
public sealed record CalculoRegistrado(
    Guid CalculoId,
    Nicho Nicho,
    Dinheiro PrecoSugerido,
    DateTimeOffset OcorridoEm) : IDomainEvent;

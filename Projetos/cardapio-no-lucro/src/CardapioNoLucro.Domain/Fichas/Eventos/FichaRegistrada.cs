using CardapioNoLucro.Domain.Common;

namespace CardapioNoLucro.Domain.Fichas.Eventos;

public sealed record FichaRegistrada(
    Guid FichaId,
    string NomeDoPrato,
    DateTimeOffset OcorridoEm) : IDomainEvent;

using Fisga.Domain.Common;

namespace Fisga.Domain.Pescarias.Eventos;

public sealed record PescariaRegistrada(
    Guid PescariaId,
    string Local,
    int Capturas,
    DateTimeOffset OcorridoEm) : IDomainEvent;

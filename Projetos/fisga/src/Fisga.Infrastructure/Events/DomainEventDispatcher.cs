using Microsoft.Extensions.Logging;
using Fisga.Application.Abstractions;
using Fisga.Domain.Common;

namespace Fisga.Infrastructure.Events;

public sealed class DomainEventDispatcher(ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    public Task PublicarAsync(IReadOnlyCollection<IDomainEvent> eventos, CancellationToken ct = default)
    {
        foreach (var evento in eventos)
        {
            logger.LogInformation(
                "Evento de domínio {Evento} em {OcorridoEm}: {@Dados}",
                evento.GetType().Name,
                evento.OcorridoEm,
                evento);
        }

        return Task.CompletedTask;
    }
}

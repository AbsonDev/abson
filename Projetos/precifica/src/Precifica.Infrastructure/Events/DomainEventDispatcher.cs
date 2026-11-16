using Microsoft.Extensions.Logging;
using Precifica.Application.Abstractions;
using Precifica.Domain.Common;

namespace Precifica.Infrastructure.Events;

/// <summary>
/// Dispatcher em processo para o MVP. A evolução para outbox garante entrega
/// após falhas e desacopla consumidores (e-mail, analytics, CRM).
/// </summary>
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

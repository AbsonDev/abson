using Precifica.Domain.Common;

namespace Precifica.Application.Abstractions;

public interface IRelogio
{
    DateTimeOffset Agora { get; }
}

public interface IDomainEventDispatcher
{
    Task PublicarAsync(IReadOnlyCollection<IDomainEvent> eventos, CancellationToken ct = default);
}

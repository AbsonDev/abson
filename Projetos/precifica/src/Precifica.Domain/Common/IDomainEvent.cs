namespace Precifica.Domain.Common;

/// <summary>
/// Algo relevante aconteceu no domínio. Por enquanto é consumido em processo
/// (log/analytics); a próxima evolução natural é um outbox transacional.
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OcorridoEm { get; }
}

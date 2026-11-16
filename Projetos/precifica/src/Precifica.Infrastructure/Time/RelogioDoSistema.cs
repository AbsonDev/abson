using Precifica.Application.Abstractions;

namespace Precifica.Infrastructure.Time;

public sealed class RelogioDoSistema : IRelogio
{
    public DateTimeOffset Agora => DateTimeOffset.UtcNow;
}

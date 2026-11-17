using Fisga.Application.Abstractions;

namespace Fisga.Infrastructure.Time;

public sealed class RelogioDoSistema : IRelogio
{
    public DateTimeOffset Agora => DateTimeOffset.UtcNow;
}

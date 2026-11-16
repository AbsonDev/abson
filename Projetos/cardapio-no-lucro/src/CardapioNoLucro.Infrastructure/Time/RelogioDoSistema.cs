using CardapioNoLucro.Application.Abstractions;

namespace CardapioNoLucro.Infrastructure.Time;

public sealed class RelogioDoSistema : IRelogio
{
    public DateTimeOffset Agora => DateTimeOffset.UtcNow;
}

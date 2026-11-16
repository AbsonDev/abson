using Microsoft.EntityFrameworkCore;
using Precifica.Application.Abstractions;
using Precifica.Domain.Pagamentos;

namespace Precifica.Infrastructure.Persistence.Repositories;

public sealed class EfPedidoRepository(PrecificaDbContext db) : IPedidoRepository
{
    public async Task AdicionarAsync(Pedido pedido, CancellationToken ct = default)
    {
        await db.Pedidos.AddAsync(pedido, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Pedido?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Pedidos.FirstOrDefaultAsync(pedido => pedido.Id == id, ct);

    public async Task<bool> ExistePagoParaCalculoAsync(Guid calculoId, CancellationToken ct = default) =>
        await db.Pedidos.AnyAsync(
            pedido => pedido.CalculoId == calculoId && pedido.Status == StatusDoPedido.Pago,
            ct);

    public async Task SalvarAsync(Pedido pedido, CancellationToken ct = default)
    {
        db.Pedidos.Update(pedido);
        await db.SaveChangesAsync(ct);
    }
}

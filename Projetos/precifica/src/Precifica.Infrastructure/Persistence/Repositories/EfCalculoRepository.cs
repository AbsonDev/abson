using Microsoft.EntityFrameworkCore;
using Precifica.Application.Abstractions;
using Precifica.Domain.Precificacao;

namespace Precifica.Infrastructure.Persistence.Repositories;

public sealed class EfCalculoRepository(PrecificaDbContext db) : ICalculoRepository
{
    public async Task AdicionarAsync(Calculo calculo, CancellationToken ct = default)
    {
        await db.Calculos.AddAsync(calculo, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Calculo?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Calculos.FirstOrDefaultAsync(calculo => calculo.Id == id, ct);

    public async Task<Calculo?> ObterPorShareTokenAsync(string shareToken, CancellationToken ct = default) =>
        await db.Calculos.FirstOrDefaultAsync(calculo => calculo.ShareToken == shareToken, ct);

    public async Task SalvarAsync(Calculo calculo, CancellationToken ct = default)
    {
        db.Calculos.Update(calculo);
        await db.SaveChangesAsync(ct);
    }
}

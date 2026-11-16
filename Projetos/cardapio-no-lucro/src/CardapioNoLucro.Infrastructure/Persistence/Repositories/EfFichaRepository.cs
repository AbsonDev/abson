using Microsoft.EntityFrameworkCore;
using CardapioNoLucro.Application.Abstractions;
using CardapioNoLucro.Domain.Fichas;

namespace CardapioNoLucro.Infrastructure.Persistence.Repositories;

public sealed class EfFichaRepository(CardapioDbContext db) : IFichaRepository
{
    public async Task AdicionarAsync(FichaTecnica ficha, CancellationToken ct = default)
    {
        await db.Fichas.AddAsync(ficha, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<FichaTecnica?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Fichas.FirstOrDefaultAsync(ficha => ficha.Id == id, ct);

    public async Task<FichaTecnica?> ObterPorShareTokenAsync(string shareToken, CancellationToken ct = default) =>
        await db.Fichas.FirstOrDefaultAsync(ficha => ficha.ShareToken == shareToken, ct);

    public async Task SalvarAsync(FichaTecnica ficha, CancellationToken ct = default)
    {
        db.Fichas.Update(ficha);
        await db.SaveChangesAsync(ct);
    }
}

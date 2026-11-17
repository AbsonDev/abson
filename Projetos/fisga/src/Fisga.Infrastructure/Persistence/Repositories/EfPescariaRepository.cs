using Microsoft.EntityFrameworkCore;
using Fisga.Application.Abstractions;
using Fisga.Domain.Pescarias;

namespace Fisga.Infrastructure.Persistence.Repositories;

public sealed class EfPescariaRepository(FisgaDbContext db) : IPescariaRepository
{
    public async Task AdicionarAsync(Pescaria pescaria, CancellationToken ct = default)
    {
        await db.Pescarias.AddAsync(pescaria, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Pescaria?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Pescarias.FirstOrDefaultAsync(pescaria => pescaria.Id == id, ct);

    public async Task<Pescaria?> ObterPorShareTokenAsync(string shareToken, CancellationToken ct = default) =>
        await db.Pescarias.FirstOrDefaultAsync(pescaria => pescaria.ShareToken == shareToken, ct);

    public async Task<IReadOnlyList<Pescaria>> ListarPorDiarioAsync(string diarioTokenHash, CancellationToken ct = default) =>
        // SQLite não ordena DateTimeOffset no servidor; a aplicação ordena os resumos.
        await db.Pescarias
            .Where(pescaria => pescaria.DiarioTokenHash == diarioTokenHash)
            .ToListAsync(ct);

    public async Task SalvarAsync(Pescaria pescaria, CancellationToken ct = default)
    {
        db.Pescarias.Update(pescaria);
        await db.SaveChangesAsync(ct);
    }
}

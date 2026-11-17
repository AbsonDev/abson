using Microsoft.EntityFrameworkCore;
using Fisga.Application.Trilhas;
using Fisga.Domain.Trilhas;

namespace Fisga.Infrastructure.Persistence.Repositories;

public sealed class EfTrilhaPrivadaRepository(FisgaDbContext db) : ITrilhaPrivadaRepository
{
    public async Task<IReadOnlyList<TrilhaPrivada>> ListarPorUsuarioAsync(
        Guid usuarioId,
        CancellationToken ct = default) =>
        await db.TrilhasPrivadas
            .Where(trilha => trilha.UsuarioId == usuarioId)
            .ToListAsync(ct);

    public async Task<TrilhaPrivada?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await db.TrilhasPrivadas.FirstOrDefaultAsync(trilha => trilha.Id == id, ct);

    public async Task<int> ContarPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default) =>
        await db.TrilhasPrivadas.CountAsync(trilha => trilha.UsuarioId == usuarioId, ct);

    public async Task AdicionarAsync(TrilhaPrivada trilha, CancellationToken ct = default)
    {
        await db.TrilhasPrivadas.AddAsync(trilha, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task SalvarAsync(TrilhaPrivada trilha, CancellationToken ct = default)
    {
        db.TrilhasPrivadas.Update(trilha);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoverAsync(TrilhaPrivada trilha, CancellationToken ct = default)
    {
        db.TrilhasPrivadas.Remove(trilha);
        await db.SaveChangesAsync(ct);
    }
}

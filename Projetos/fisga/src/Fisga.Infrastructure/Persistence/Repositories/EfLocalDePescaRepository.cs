using Microsoft.EntityFrameworkCore;
using Fisga.Application.Locais;
using Fisga.Domain.Locais;

namespace Fisga.Infrastructure.Persistence.Repositories;

public sealed class EfLocalDePescaRepository(FisgaDbContext db) : ILocalDePescaRepository
{
    public async Task<IReadOnlyList<LocalDePesca>> ListarPorUsuarioAsync(
        Guid usuarioId,
        CancellationToken ct = default) =>
        await db.LocaisDePesca
            .Where(local => local.UsuarioId == usuarioId)
            .ToListAsync(ct);

    public async Task<LocalDePesca?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await db.LocaisDePesca.FirstOrDefaultAsync(local => local.Id == id, ct);

    public async Task<int> ContarPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default) =>
        await db.LocaisDePesca.CountAsync(local => local.UsuarioId == usuarioId, ct);

    public async Task AdicionarAsync(LocalDePesca local, CancellationToken ct = default)
    {
        await db.LocaisDePesca.AddAsync(local, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task SalvarAsync(LocalDePesca local, CancellationToken ct = default)
    {
        db.LocaisDePesca.Update(local);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoverAsync(LocalDePesca local, CancellationToken ct = default)
    {
        db.LocaisDePesca.Remove(local);
        await db.SaveChangesAsync(ct);
    }

    public async Task DesmarcarPadraoAsync(Guid usuarioId, CancellationToken ct = default) =>
        await db.LocaisDePesca
            .Where(local => local.UsuarioId == usuarioId && local.Padrao)
            .ExecuteUpdateAsync(atualizacao => atualizacao.SetProperty(local => local.Padrao, false), ct);
}

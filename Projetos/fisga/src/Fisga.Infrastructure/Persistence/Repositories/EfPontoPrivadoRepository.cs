using Microsoft.EntityFrameworkCore;
using Fisga.Application.Pontos;
using Fisga.Domain.Pontos;

namespace Fisga.Infrastructure.Persistence.Repositories;

public sealed class EfPontoPrivadoRepository(FisgaDbContext db) : IPontoPrivadoRepository
{
    public async Task<IReadOnlyList<PontoPrivado>> ListarPorUsuarioAsync(
        Guid usuarioId,
        CancellationToken ct = default) =>
        await db.PontosPrivados
            .Where(ponto => ponto.UsuarioId == usuarioId)
            .ToListAsync(ct);

    public async Task<PontoPrivado?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await db.PontosPrivados.FirstOrDefaultAsync(ponto => ponto.Id == id, ct);

    public async Task<int> ContarPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default) =>
        await db.PontosPrivados.CountAsync(ponto => ponto.UsuarioId == usuarioId, ct);

    public async Task AdicionarAsync(PontoPrivado ponto, CancellationToken ct = default)
    {
        await db.PontosPrivados.AddAsync(ponto, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task SalvarAsync(PontoPrivado ponto, CancellationToken ct = default)
    {
        db.PontosPrivados.Update(ponto);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoverAsync(PontoPrivado ponto, CancellationToken ct = default)
    {
        db.PontosPrivados.Remove(ponto);
        await db.SaveChangesAsync(ct);
    }
}

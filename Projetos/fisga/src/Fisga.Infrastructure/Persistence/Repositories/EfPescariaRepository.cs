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

    public async Task<IReadOnlyList<Pescaria>> ListarPorUsuarioAsync(
        Guid usuarioId,
        CancellationToken ct = default) =>
        await db.Pescarias
            .Where(pescaria => pescaria.UsuarioId == usuarioId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> RemoverPorUsuarioAsync(
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var pescarias = await db.Pescarias
            .Where(pescaria => pescaria.UsuarioId == usuarioId)
            .ToListAsync(ct);

        var fotos = pescarias
            .SelectMany(pescaria => pescaria.Capturas)
            .Select(captura => captura.FotoUrl)
            .Where(foto => !string.IsNullOrWhiteSpace(foto))
            .Select(foto => foto!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        db.Pescarias.RemoveRange(pescarias);
        return fotos;
    }

    public async Task SalvarAsync(Pescaria pescaria, CancellationToken ct = default)
    {
        db.Pescarias.Update(pescaria);
        await db.SaveChangesAsync(ct);
    }
}

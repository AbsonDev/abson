using Microsoft.EntityFrameworkCore;
using Fisga.Application.Identidade;
using Fisga.Domain.Identidade;

namespace Fisga.Infrastructure.Persistence.Repositories;

public sealed class EfSessaoRepository(FisgaDbContext db) : ISessaoRepository
{
    public async Task<Sessao?> ObterPorHashAsync(string tokenHash, CancellationToken ct = default) =>
        await db.Sessoes.FirstOrDefaultAsync(sessao => sessao.TokenHash == tokenHash, ct);

    public async Task<IReadOnlyList<Sessao>> ListarAtivasAsync(
        Guid usuarioId,
        DateTimeOffset agora,
        CancellationToken ct = default)
    {
        // SQLite não compara nem ordena DateTimeOffset no servidor; filtramos na aplicação.
        var sessoes = await db.Sessoes
            .Where(sessao => sessao.UsuarioId == usuarioId && sessao.RevogadaEm == null)
            .ToListAsync(ct);

        return sessoes
            .Where(sessao => sessao.ExpiraEm > agora)
            .OrderByDescending(sessao => sessao.UltimoUsoEm)
            .ToList();
    }

    public void Adicionar(Sessao sessao) => db.Sessoes.Add(sessao);

    public async Task RevogarTodasAsync(
        Guid usuarioId,
        DateTimeOffset agora,
        Guid? exceto,
        CancellationToken ct = default)
    {
        var sessoes = await db.Sessoes
            .Where(sessao => sessao.UsuarioId == usuarioId
                             && sessao.RevogadaEm == null
                             && (exceto == null || sessao.Id != exceto))
            .ToListAsync(ct);

        foreach (var sessao in sessoes)
        {
            sessao.Revogar(agora);
        }
    }
}

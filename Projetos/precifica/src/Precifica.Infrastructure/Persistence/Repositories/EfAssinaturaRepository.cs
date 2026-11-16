using Microsoft.EntityFrameworkCore;
using Precifica.Application.Abstractions;
using Precifica.Domain.Pagamentos;

namespace Precifica.Infrastructure.Persistence.Repositories;

public sealed class EfAssinaturaRepository(PrecificaDbContext db) : IAssinaturaRepository
{
    public async Task AdicionarAsync(Assinatura assinatura, CancellationToken ct = default)
    {
        await db.Assinaturas.AddAsync(assinatura, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Assinatura?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Assinaturas.FirstOrDefaultAsync(assinatura => assinatura.Id == id, ct);

    public async Task<Assinatura?> ObterPorEmailAsync(string email, CancellationToken ct = default)
    {
        // SQLite não ordena DateTimeOffset no servidor; o volume por e-mail é pequeno,
        // então ordenamos em memória para funcionar igual no desenvolvimento e no Postgres.
        var candidatas = await db.Assinaturas
            .Where(assinatura => assinatura.Email == email && assinatura.Status == StatusDaAssinatura.Ativa)
            .ToListAsync(ct);

        return candidatas.OrderByDescending(assinatura => assinatura.CriadaEm).FirstOrDefault();
    }

    public async Task<Assinatura?> ObterPorReferenciaGatewayAsync(string referencia, CancellationToken ct = default) =>
        await db.Assinaturas.FirstOrDefaultAsync(assinatura => assinatura.ReferenciaGateway == referencia, ct);

    public async Task SalvarAsync(Assinatura assinatura, CancellationToken ct = default)
    {
        db.Assinaturas.Update(assinatura);
        await db.SaveChangesAsync(ct);
    }
}

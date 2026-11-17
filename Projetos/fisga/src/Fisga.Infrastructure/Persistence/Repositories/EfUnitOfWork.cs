using Fisga.Application.Identidade;

namespace Fisga.Infrastructure.Persistence.Repositories;

public sealed class EfUnitOfWork(FisgaDbContext db) : IUnitOfWork
{
    public Task<int> SalvarAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

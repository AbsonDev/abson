using Microsoft.EntityFrameworkCore;
using Fisga.Domain.Pescarias;

namespace Fisga.Infrastructure.Persistence;

public sealed class FisgaDbContext(DbContextOptions<FisgaDbContext> options) : DbContext(options)
{
    public DbSet<Pescaria> Pescarias => Set<Pescaria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FisgaDbContext).Assembly);
    }
}

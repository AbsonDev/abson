using Microsoft.EntityFrameworkCore;
using Fisga.Domain.Identidade;
using Fisga.Domain.Pescarias;

namespace Fisga.Infrastructure.Persistence;

public sealed class FisgaDbContext(DbContextOptions<FisgaDbContext> options) : DbContext(options)
{
    public DbSet<Pescaria> Pescarias => Set<Pescaria>();

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Sessao> Sessoes => Set<Sessao>();

    public DbSet<TokenDeLogin> TokensDeLogin => Set<TokenDeLogin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FisgaDbContext).Assembly);
    }
}

using Microsoft.EntityFrameworkCore;
using CardapioNoLucro.Domain.Fichas;

namespace CardapioNoLucro.Infrastructure.Persistence;

public sealed class CardapioDbContext(DbContextOptions<CardapioDbContext> options) : DbContext(options)
{
    public DbSet<FichaTecnica> Fichas => Set<FichaTecnica>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CardapioDbContext).Assembly);
    }
}

using Microsoft.EntityFrameworkCore;
using Precifica.Domain.Pagamentos;
using Precifica.Domain.Precificacao;

namespace Precifica.Infrastructure.Persistence;

public sealed class PrecificaDbContext(DbContextOptions<PrecificaDbContext> options) : DbContext(options)
{
    public DbSet<Calculo> Calculos => Set<Calculo>();

    public DbSet<Pedido> Pedidos => Set<Pedido>();

    public DbSet<Assinatura> Assinaturas => Set<Assinatura>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PrecificaDbContext).Assembly);
    }
}

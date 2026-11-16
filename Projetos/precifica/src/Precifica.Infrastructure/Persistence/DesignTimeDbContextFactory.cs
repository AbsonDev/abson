using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Precifica.Infrastructure.Persistence;

/// <summary>
/// Usado apenas pelo `dotnet ef` para gerar migrations. A aplicação configura
/// o provider em <see cref="DependencyInjection.AdicionarInfraestrutura"/>.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PrecificaDbContext>
{
    public PrecificaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PrecificaDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=precifica;Username=precifica;Password=precifica")
            .Options;

        return new PrecificaDbContext(options);
    }
}

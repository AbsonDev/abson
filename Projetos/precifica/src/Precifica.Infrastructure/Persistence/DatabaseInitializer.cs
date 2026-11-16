using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Precifica.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InicializarBancoAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrecificaDbContext>();

        if (db.Database.IsSqlite())
        {
            // Zero-config no desenvolvimento local; migrations reais rodam no Postgres.
            await db.Database.EnsureCreatedAsync(ct);
            return;
        }

        await db.Database.MigrateAsync(ct);
    }
}

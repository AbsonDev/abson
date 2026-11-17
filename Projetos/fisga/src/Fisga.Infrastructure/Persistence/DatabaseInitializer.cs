using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fisga.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InicializarBancoAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FisgaDbContext>();

        if (db.Database.IsSqlite())
        {
            await db.Database.EnsureCreatedAsync(ct);
            return;
        }

        await db.Database.MigrateAsync(ct);
    }
}

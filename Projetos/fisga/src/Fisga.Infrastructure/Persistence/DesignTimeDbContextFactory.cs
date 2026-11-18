using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fisga.Infrastructure.Persistence;

/// <summary>
/// Contexto usado apenas pelo `dotnet ef` (migrations). Lê as mesmas variáveis
/// de ambiente da API para que o ensaio local aponte para o Postgres do compose
/// (porta 5435), não para um 5432 implícito. Nunca contém segredo de produção.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FisgaDbContext>
{
    public FisgaDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("Database__Provider") ?? "Postgres";
        var connectionString = Environment.GetEnvironmentVariable("Database__ConnectionString")
            ?? "Host=localhost;Port=5435;Database=fisga;Username=fisga;Password=fisga";

        var options = new DbContextOptionsBuilder<FisgaDbContext>();
        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            options.UseSqlite(connectionString);
        }
        else
        {
            options.UseNpgsql(connectionString);
        }

        return new FisgaDbContext(options.Options);
    }
}

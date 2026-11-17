using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Fisga.Application.Abstractions;
using Fisga.Infrastructure.Events;
using Fisga.Infrastructure.Midia;
using Fisga.Infrastructure.Pdf;
using Fisga.Infrastructure.Persistence;
using Fisga.Infrastructure.Persistence.Repositories;
using Fisga.Infrastructure.Time;
using Fisga.Infrastructure.Payments;

namespace Fisga.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AdicionarInfraestrutura(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<WebOptions>(configuration.GetSection("Web"));

        var provider = configuration["Database:Provider"] ?? "Postgres";
        var connectionString = configuration["Database:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Configure Database:ConnectionString (ou Database__ConnectionString no ambiente).");
        }

        services.AddDbContext<FisgaDbContext>(options =>
        {
            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseNpgsql(connectionString);
            }
        });

        services.AddScoped<IPescariaRepository, EfPescariaRepository>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IMidiaStorage, ArmazenamentoDeMidiaLocal>();
        services.AddSingleton<IRelogio, RelogioDoSistema>();
        services.AddSingleton<IGeradorDePdf, QuestPdfGerador>();

        return services;
    }
}

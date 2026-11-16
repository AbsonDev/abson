using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using CardapioNoLucro.Application.Abstractions;
using CardapioNoLucro.Infrastructure.Events;
using CardapioNoLucro.Infrastructure.Pdf;
using CardapioNoLucro.Infrastructure.Persistence;
using CardapioNoLucro.Infrastructure.Persistence.Repositories;
using CardapioNoLucro.Infrastructure.Time;
using CardapioNoLucro.Infrastructure.Payments;

namespace CardapioNoLucro.Infrastructure;

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

        services.AddDbContext<CardapioDbContext>(options =>
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

        services.AddScoped<IFichaRepository, EfFichaRepository>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton<IRelogio, RelogioDoSistema>();
        services.AddSingleton<IGeradorDePdf, QuestPdfGerador>();

        return services;
    }
}

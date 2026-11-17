using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Fisga.Application.Abstractions;
using Fisga.Application.Identidade;
using Fisga.Application.Newsletter;
using Fisga.Infrastructure.Emails;
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

        services.Configure<AutenticacaoOptions>(configuration.GetSection("Auth"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<AutenticacaoOptions>>().Value);

        services.Configure<EmailOptions>(configuration.GetSection("Email"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<EmailOptions>>().Value);

        services.Configure<NewsletterOptions>(configuration.GetSection("Newsletter"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<NewsletterOptions>>().Value);

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
        services.AddScoped<IUsuarioRepository, EfUsuarioRepository>();
        services.AddScoped<ISessaoRepository, EfSessaoRepository>();
        services.AddScoped<ITokenDeLoginRepository, EfTokenDeLoginRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IMidiaStorage, ArmazenamentoDeMidiaLocal>();
        services.AddSingleton<IRelogio, RelogioDoSistema>();
        services.AddSingleton<IGeradorDePdf, QuestPdfGerador>();

        services.AddSingleton<ConsoleEmailSender>();
        services.AddHttpClient<ResendEmailSender>();
        services.AddScoped<IEmailSender>(sp =>
        {
            var opcoes = sp.GetRequiredService<EmailOptions>();
            return string.Equals(opcoes.Provider, "Resend", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(opcoes.ResendApiKey)
                ? sp.GetRequiredService<ResendEmailSender>()
                : sp.GetRequiredService<ConsoleEmailSender>();
        });

        return services;
    }
}

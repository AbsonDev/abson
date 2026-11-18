using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Fisga.Application.Abstractions;
using Fisga.Application.Condicoes;
using Fisga.Application.Identidade;
using Fisga.Application.Locais;
using Fisga.Application.Pontos;
using Fisga.Application.Trilhas;
using Fisga.Application.Vision;
using Fisga.Application.Newsletter;
using Fisga.Infrastructure.Condicoes;
using Fisga.Infrastructure.Emails;
using Fisga.Infrastructure.Events;
using Fisga.Infrastructure.Identity;
using Fisga.Infrastructure.Midia;
using Fisga.Infrastructure.Pdf;
using Fisga.Infrastructure.Persistence;
using Fisga.Infrastructure.Persistence.Repositories;
using Fisga.Infrastructure.Time;
using Fisga.Infrastructure.Vision;
using Fisga.Infrastructure.Payments;

namespace Fisga.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AdicionarInfraestrutura(
        this IServiceCollection services,
        IConfiguration configuration,
        bool desenvolvimento)
    {
        services.Configure<WebOptions>(configuration.GetSection("Web"));

        services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(20),
                LocalCacheExpiration = TimeSpan.FromMinutes(5),
            };
        });

        services.Configure<AutenticacaoOptions>(configuration.GetSection("Auth"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<AutenticacaoOptions>>().Value);

        services.Configure<EmailOptions>(configuration.GetSection("Email"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<EmailOptions>>().Value);

        services.Configure<NewsletterOptions>(configuration.GetSection("Newsletter"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<NewsletterOptions>>().Value);

        services.Configure<CondicoesOptions>(configuration.GetSection("Condicoes"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<CondicoesOptions>>().Value);

        services.Configure<VisionOptions>(configuration.GetSection("Vision"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<VisionOptions>>().Value);

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
        services.AddScoped<IConsentimentoRepository, EfConsentimentoRepository>();
        services.AddScoped<ILocalDePescaRepository, EfLocalDePescaRepository>();
        services.AddScoped<IPontoPrivadoRepository, EfPontoPrivadoRepository>();
        services.AddScoped<ITrilhaPrivadaRepository, EfTrilhaPrivadaRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<ICadastroDeUsuario, CadastroDeUsuario>();
        services.AddScoped<IExclusaoDeIdentidade, ExclusaoDeIdentidade>();
        services.AddScoped<GoogleIdentityValidator>();
        services.AddScoped<GoogleIdentityService>();
        services.AddScoped<IMidiaStorage, ArmazenamentoDeMidiaLocal>();
        services.AddSingleton<IRelogio, RelogioDoSistema>();
        services.AddSingleton<IGeradorDePdf, QuestPdfGerador>();

        services.AddHttpClient<OpenMeteoProvedorDeCondicoes>(cliente =>
        {
            cliente.Timeout = TimeSpan.FromSeconds(12);
            cliente.DefaultRequestHeaders.UserAgent.ParseAdd("Fisga/1.0 (+https://fisga.abson.tech)");
        });
        services.AddHttpClient<OpenMeteoGeocodificador>(cliente =>
        {
            cliente.Timeout = TimeSpan.FromSeconds(10);
            cliente.DefaultRequestHeaders.UserAgent.ParseAdd("Fisga/1.0 (+https://fisga.abson.tech)");
        });
        services.AddHttpClient<GeminiReconhecedor>(cliente =>
        {
            cliente.Timeout = TimeSpan.FromSeconds(35);
            cliente.DefaultRequestHeaders.UserAgent.ParseAdd("Fisga/1.0 (+https://fisga.abson.tech)");
        });
        services.AddSingleton<ManualReconhecedor>();
        services.AddScoped<IReconhecedorDeEspecie>(servicos =>
        {
            var opcoes = servicos.GetRequiredService<VisionOptions>();
            return string.Equals(opcoes.Provider, "Gemini", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(opcoes.ApiKey)
                ? servicos.GetRequiredService<GeminiReconhecedor>()
                : servicos.GetRequiredService<ManualReconhecedor>();
        });
        services.AddScoped<IProvedorDeCondicoes>(servicos =>
            servicos.GetRequiredService<OpenMeteoProvedorDeCondicoes>());
        services.AddScoped<IGeocodificador>(servicos =>
            servicos.GetRequiredService<OpenMeteoGeocodificador>());

        services.AddSingleton<ConsoleEmailSender>();
        if (desenvolvimento)
        {
            // Defesa em profundidade: o outbox grava tokens em arquivo e sequer
            // é registrado fora de Development. O guard de startup continua ativo.
            services.AddSingleton<OutboxEmailSender>();
        }
        services.AddHttpClient<ResendEmailSender>();
        services.AddScoped<IEmailSender>(sp =>
        {
            var opcoes = sp.GetRequiredService<EmailOptions>();
            if (string.Equals(opcoes.Provider, "Outbox", StringComparison.OrdinalIgnoreCase))
            {
                if (!desenvolvimento)
                {
                    throw new InvalidOperationException(
                        "Email:Provider=Outbox é exclusivo de Development.");
                }

                return sp.GetRequiredService<OutboxEmailSender>();
            }

            return string.Equals(opcoes.Provider, "Resend", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(opcoes.ResendApiKey)
                ? sp.GetRequiredService<ResendEmailSender>()
                : sp.GetRequiredService<ConsoleEmailSender>();
        });
        services.AddScoped<EmailDeIdentidade>();

        services.AdicionarIdentity(configuration);

        return services;
    }
}

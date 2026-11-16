using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Precifica.Application.Abstractions;
using Precifica.Infrastructure.Events;
using Precifica.Infrastructure.Payments;
using Precifica.Infrastructure.Pdf;
using Precifica.Infrastructure.Persistence;
using Precifica.Infrastructure.Persistence.Repositories;
using Precifica.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace Precifica.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AdicionarInfraestrutura(
        this IServiceCollection services,
        IConfiguration configuration,
        bool desenvolvimento)
    {
        services.Configure<MercadoPagoOptions>(configuration.GetSection("MercadoPago"));
        services.Configure<PagamentoOptions>(configuration.GetSection("Pagamentos"));
        services.Configure<WebOptions>(configuration.GetSection("Web"));
        services.Configure<Application.Pagamentos.MonetizacaoOptions>(configuration.GetSection("Monetizacao"));

        var provider = configuration["Database:Provider"] ?? "Postgres";
        var connectionString = configuration["Database:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Configure Database:ConnectionString (ou Database__ConnectionString no ambiente).");
        }

        services.AddDbContext<PrecificaDbContext>(options =>
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

        services.AddScoped<ICalculoRepository, EfCalculoRepository>();
        services.AddScoped<IPedidoRepository, EfPedidoRepository>();
        services.AddScoped<IAssinaturaRepository, EfAssinaturaRepository>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton<IRelogio, RelogioDoSistema>();
        services.AddSingleton<IGeradorDePdf, QuestPdfGerador>();

        var modoSimulado = string.Equals(
            configuration["Pagamentos:Modo"],
            "Simulado",
            StringComparison.OrdinalIgnoreCase);

        if (modoSimulado && !desenvolvimento)
        {
            throw new InvalidOperationException(
                "Pagamentos:Modo=Simulado só pode ser usado em Development. Configure o Mercado Pago em produção.");
        }

        if (modoSimulado)
        {
            services.AddScoped<IGatewayDePagamento, GatewaySimulado>();
        }
        else
        {
            var baseUrl = configuration["MercadoPago:BaseUrl"] ?? "https://api.mercadopago.com";
            services.AddHttpClient<IGatewayDePagamento, MercadoPagoGateway>(cliente =>
            {
                cliente.BaseAddress = new Uri(baseUrl);
                cliente.Timeout = TimeSpan.FromSeconds(30);
            });
        }

        return services;
    }
}

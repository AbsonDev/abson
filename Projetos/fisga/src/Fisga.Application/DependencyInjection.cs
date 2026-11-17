using Microsoft.Extensions.DependencyInjection;
using Fisga.Application.Catalogo;
using Fisga.Application.Documentos;
using Fisga.Application.Identidade;
using Fisga.Application.Newsletter;
using Fisga.Application.Pescarias;

namespace Fisga.Application;

public static class DependencyInjection
{
    public static IServiceCollection AdicionarAplicacao(this IServiceCollection services)
    {
        services.AddSingleton<ICatalogoDePesca, CatalogoDePesca>();
        services.AddScoped<ServicoDePescarias>();
        services.AddScoped<ServicoDoDiario>();
        services.AddScoped<ServicoDePdf>();
        services.AddScoped<ServicoDeAutenticacao>();
        services.AddScoped<ServicoDeConta>();
        services.AddScoped<ServicoDeNewsletter>();
        return services;
    }
}

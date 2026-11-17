using Microsoft.Extensions.DependencyInjection;
using Fisga.Application.Catalogo;
using Fisga.Application.Comparacao;
using Fisga.Application.Condicoes;
using Fisga.Application.Documentos;
using Fisga.Application.Identidade;
using Fisga.Application.Locais;
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
        services.AddScoped<ServicoDeLocais>();
        services.AddScoped<ServicoDeCondicoes>();
        services.AddScoped<ServicoDeComparacao>();
        return services;
    }
}

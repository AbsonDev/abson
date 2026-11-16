using Microsoft.Extensions.DependencyInjection;
using CardapioNoLucro.Application.Catalogo;
using CardapioNoLucro.Application.Documentos;
using CardapioNoLucro.Application.Fichas;

namespace CardapioNoLucro.Application;

public static class DependencyInjection
{
    public static IServiceCollection AdicionarAplicacao(this IServiceCollection services)
    {
        services.AddSingleton<ICatalogoDeCanais, CatalogoDeCanais>();
        services.AddScoped<ServicoDeFichas>();
        services.AddScoped<ServicoDePdf>();
        return services;
    }
}

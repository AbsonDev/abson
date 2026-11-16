using Microsoft.Extensions.DependencyInjection;
using Precifica.Application.Calculos;
using Precifica.Application.Catalogo;
using Precifica.Application.Documentos;
using Precifica.Application.Pagamentos;

namespace Precifica.Application;

public static class DependencyInjection
{
    public static IServiceCollection AdicionarAplicacao(this IServiceCollection services)
    {
        services.AddSingleton<ICatalogoDeNichos, CatalogoDeNichos>();
        services.AddScoped<ServicoDeCalculos>();
        services.AddScoped<ServicoDeAcesso>();
        services.AddScoped<ServicoDePagamentos>();
        services.AddScoped<ServicoDePdf>();
        return services;
    }
}

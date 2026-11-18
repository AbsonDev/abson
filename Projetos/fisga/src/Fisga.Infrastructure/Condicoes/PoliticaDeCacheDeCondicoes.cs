using Fisga.Application.Condicoes;

namespace Fisga.Infrastructure.Condicoes;

public static class PoliticaDeCacheDeCondicoes
{
    private static readonly TimeSpan Completo = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan Parcial = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Indisponivel = TimeSpan.FromSeconds(30);

    public static TimeSpan Ttl(DadosDeCondicoes? dados) =>
        dados is null
            ? Indisponivel
            : !dados.TemMare || dados.Lua is null
                ? Parcial
                : Completo;
}

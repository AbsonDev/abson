using Fisga.Application.Observabilidade;

namespace Fisga.Infrastructure.Observabilidade;

/// <summary>
/// Implementação neutra para composições sem telemetria (testes, ferramentas).
/// A Api registra o adaptador de métricas por cima desta.
/// </summary>
public sealed class CacheObservabilidadeNula : ICacheObservabilidade
{
    public void CondicoesUpstream()
    {
    }

    public void GeocodingUpstream(string tipo)
    {
    }

    public void GeocodingNegativo(string tipo)
    {
    }
}

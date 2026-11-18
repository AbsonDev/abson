using Fisga.Application.Observabilidade;

namespace Fisga.Api.Observabilidade;

/// <summary>
/// Adaptador de telemetria do cache. Só a Api conhece o Sentry; a Infrastructure
/// depende apenas de <see cref="ICacheObservabilidade"/>.
/// </summary>
public sealed class SentryCacheObservabilidade : ICacheObservabilidade
{
    public void CondicoesUpstream() => Metricas.CondicoesUpstream();

    public void GeocodingUpstream(string tipo) => Metricas.GeocodingUpstream(tipo);

    public void GeocodingNegativo(string tipo) => Metricas.GeocodingNegativo(tipo);
}

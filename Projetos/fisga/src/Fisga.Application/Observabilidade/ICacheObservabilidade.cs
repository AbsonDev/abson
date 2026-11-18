namespace Fisga.Application.Observabilidade;

/// <summary>
/// Métricas de cache e upstream com atributos de baixa cardinalidade. Nunca
/// receba coordenadas, termo de busca, e-mail, cookie ou token.
/// </summary>
public interface ICacheObservabilidade
{
    void CondicoesUpstream();

    void GeocodingUpstream(string tipo);

    void GeocodingNegativo(string tipo);
}

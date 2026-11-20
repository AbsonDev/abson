using Microsoft.Extensions.Configuration;

namespace Fisga.Api.RateLimiting;

/// <summary>
/// Fonte única das políticas de rate limit: nomes e limites padrão por minuto.
/// A configuração (<c>RateLimiting:&lt;política&gt;</c>) vence o padrão; o contrato
/// publicado e os testes leem daqui para não divergirem do comportamento.
/// </summary>
public static class PoliticasDeTaxa
{
    public const string Leitura = "leitura";
    public const string Escrita = "escrita";
    public const string Auth = "auth";
    public const string Pdf = "pdf";
    public const string Midia = "midia";

    // Janela fixa de um minuto (FixedWindowRateLimiterOptions.Window).
    public const int JanelaEmSegundos = 60;

    public static IReadOnlyDictionary<string, int> PadroesPorMinuto { get; } =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [Leitura] = 180,
            [Escrita] = 40,
            [Auth] = 8,
            [Pdf] = 30,
            [Midia] = 30
        };

    public static int LimiteDe(IConfiguration configuracao, string politica, string? contexto = null) =>
        configuracao.GetValue<int?>($"RateLimiting:{politica}")
        ?? (PadroesPorMinuto.TryGetValue(politica, out var padrao)
            ? padrao
            : throw new InvalidOperationException(
                $"Política sem limite conhecido: {politica}"
                + (contexto is null ? string.Empty : $" ({contexto})")));
}

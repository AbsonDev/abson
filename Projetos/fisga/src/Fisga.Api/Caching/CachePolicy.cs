namespace Fisga.Api.Caching;

// Política de cache aplicada por endpoint via metadata. Ausência de metadata
// significa "não cachear": o CacheHeadersMiddleware cai em NoStore.
public sealed record CachePolicy(string HeaderValue)
{
    public static CachePolicy NoStore { get; } = new("private, no-store");

    public static CachePolicy Public(int maxAgeSeconds, int swr = 0) =>
        new(Montar("public", maxAgeSeconds, swr));

    public static CachePolicy Private(int maxAgeSeconds, int swr = 0) =>
        new(Montar("private", maxAgeSeconds, swr));

    private static string Montar(string visibilidade, int maxAgeSeconds, int swr) =>
        swr > 0
            ? $"{visibilidade}, max-age={maxAgeSeconds}, stale-while-revalidate={swr}"
            : $"{visibilidade}, max-age={maxAgeSeconds}";
}

/// <summary>
/// Decide o Cache-Control final a partir do status. Erro (>= 400) nunca pode
/// herdar o TTL positivo do endpoint: um 404, 429 ou 500 com max-age ficaria
/// pinado no navegador/CDN mesmo depois de o recurso voltar.
/// </summary>
public static class DecisaoDeCache
{
    public const string NoStore = "private, no-store";

    public static string Valor(int statusCode, string politica) =>
        statusCode >= 400 ? NoStore : politica;
}

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

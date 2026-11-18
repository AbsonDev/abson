namespace Fisga.Api.Caching;

// Default-deny de cache: aplica a política do endpoint (metadata CachePolicy) e,
// quando não há nenhuma, responde "private, no-store". Um Cache-Control já
// definido pelo endpoint (ou por outro middleware antes do envio) é preservado
// em respostas de sucesso; erros (>= 400) sempre saem como "private, no-store".
public sealed class CacheHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var politica = context.GetEndpoint()?.Metadata.GetMetadata<CachePolicy>()
                       ?? CachePolicy.NoStore;

        context.Response.OnStarting(() =>
        {
            if (context.Response.StatusCode >= 400)
            {
                context.Response.Headers.CacheControl = DecisaoDeCache.NoStore;
            }
            else if (!context.Response.Headers.ContainsKey(Headers.CacheControl))
            {
                context.Response.Headers.CacheControl = politica.HeaderValue;
            }

            return Task.CompletedTask;
        });

        await next(context);
    }

    private static class Headers
    {
        public const string CacheControl = "Cache-Control";
    }
}

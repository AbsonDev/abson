using Fisga.Api.Contracts;
using Fisga.Api.OpenApi;

namespace Fisga.Api.Middleware;

/// <summary>
/// Defesa em profundidade contra CSRF: além do cookie SameSite=Lax e do CORS
/// restrito, rejeita mutações com Origin de outro site.
/// </summary>
public sealed class ProtecaoCsrfMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private readonly string[] _origens = (configuration["Cors:AllowedOrigins"] ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api")
            && MetodosHttp.EhMutacao(context.Request.Method))
        {
            var origem = context.Request.Headers.Origin.ToString();
            if (!string.IsNullOrWhiteSpace(origem)
                && !_origens.Contains(origem, StringComparer.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(
                    new ErroResponse(CodigosDeErro.OrigemInvalida, DetalhesDeErro.OrigemInvalida));
                return;
            }
        }

        await next(context);
    }
}

using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace Fisga.Api.Observabilidade;

/// <summary>
/// Mede a duração das rotas /api sem usar o caminho bruto (que pode conter IDs ou tokens).
/// </summary>
public sealed class MetricasDeRequisicaoMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        if (!contexto.Request.Path.StartsWithSegments("/api"))
        {
            await next(contexto);
            return;
        }

        var inicio = Stopwatch.GetTimestamp();
        try
        {
            await next(contexto);
        }
        finally
        {
            var rota = (contexto.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
            if (!string.IsNullOrWhiteSpace(rota))
            {
                Metricas.RequisicaoApi(
                    rota,
                    contexto.Request.Method,
                    contexto.Response.StatusCode,
                    Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);
            }
        }
    }
}

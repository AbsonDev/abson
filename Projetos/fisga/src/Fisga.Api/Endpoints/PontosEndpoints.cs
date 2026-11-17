using Fisga.Application.Pontos;
using Fisga.Domain.Pontos;

namespace Fisga.Api.Endpoints;

public static class PontosEndpoints
{
    public static IEndpointRouteBuilder MapPontosEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/pontos");

        grupo.MapGet("", async (
                HttpContext http,
                ServicoDePontos servico,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var lista = await servico.ListarAsync(usuarioId, ct);
                return Results.Ok(lista);
            })
            .RequireRateLimiting("leitura");

        grupo.MapPost("", async (
                EntradaDePonto entrada,
                HttpContext http,
                ServicoDePontos servico,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var ponto = await servico.CriarAsync(entrada, usuarioId, ct);
                return Results.Created($"/api/pontos/{ponto.Id}", ponto);
            })
            .RequireRateLimiting("escrita");

        grupo.MapPut("/{id:guid}", async (
                Guid id,
                EntradaDePonto entrada,
                HttpContext http,
                ServicoDePontos servico,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var ponto = await servico.AtualizarAsync(id, entrada, usuarioId, ct);
                return Results.Ok(ponto);
            })
            .RequireRateLimiting("escrita");

        grupo.MapDelete("/{id:guid}", async (
                Guid id,
                HttpContext http,
                ServicoDePontos servico,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                await servico.ExcluirAsync(id, usuarioId, ct);
                return Results.NoContent();
            })
            .RequireRateLimiting("escrita");

        return app;
    }
}

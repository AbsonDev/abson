using Fisga.Application.Trilhas;

namespace Fisga.Api.Endpoints;

public static class TrilhasEndpoints
{
    public static IEndpointRouteBuilder MapTrilhasEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/trilhas");

        grupo.MapGet("", async (
                HttpContext http,
                ServicoDeTrilhas servico,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var lista = await servico.ListarAsync(usuarioId, ct);
                return Results.Ok(lista);
            })
            .RequireRateLimiting("leitura");

        grupo.MapPost("", async (
                EntradaDeTrilha entrada,
                HttpContext http,
                ServicoDeTrilhas servico,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var trilha = await servico.CriarAsync(entrada, usuarioId, ct);
                return Results.Created($"/api/trilhas/{trilha.Id}", trilha);
            })
            .RequireRateLimiting("escrita");

        grupo.MapPut("/{id:guid}", async (
                Guid id,
                EntradaDeTrilha entrada,
                HttpContext http,
                ServicoDeTrilhas servico,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var trilha = await servico.AtualizarAsync(id, entrada, usuarioId, ct);
                return Results.Ok(trilha);
            })
            .RequireRateLimiting("escrita");

        grupo.MapDelete("/{id:guid}", async (
                Guid id,
                HttpContext http,
                ServicoDeTrilhas servico,
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

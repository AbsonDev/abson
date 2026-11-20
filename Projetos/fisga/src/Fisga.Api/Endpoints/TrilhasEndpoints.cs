using Fisga.Api.OpenApi;
using Fisga.Application.Trilhas;

namespace Fisga.Api.Endpoints;

public static class TrilhasEndpoints
{
    public static IEndpointRouteBuilder MapTrilhasEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/trilhas").WithTags(Tags.Trilhas);

        grupo.MapGet("", async (
                HttpContext http,
                ServicoDeTrilhas servico,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var lista = await servico.ListarAsync(usuarioId, ct);
                return Results.Ok(lista);
            })
            .Produces<IReadOnlyList<TrilhaDto>>(200)
            .ComErro(401)
            .ComErro(403)
            .SemCorpo(429)
            .RequireRateLimiting("leitura")
            .WithSummary("Lista as trilhas privadas")
            .WithDescription("Exige sessão e retorna apenas as trilhas da conta, da mais recente para a mais antiga.");

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
            .Produces<TrilhaDto>(201)
            .ComErro(400)
            .ComErro(401)
            .ComErro(403)
            .SemCorpo(429)
            .RequireRateLimiting("escrita")
            .WithSummary("Salva uma trilha privada")
            .WithDescription("Exige sessão. Cada conta pode manter até cem trilhas privadas.");

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
            .Produces<TrilhaDto>(200)
            .ComErro(400)
            .ComErro(401)
            .ComErro(403)
            .ComErro(404)
            .SemCorpo(429)
            .RequireRateLimiting("escrita")
            .WithSummary("Atualiza uma trilha privada")
            .WithDescription("Exige sessão e aceita apenas o dono da trilha. Retorna 404 quando a trilha não existe.");

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
            .SemCorpo(204)
            .ComErro(401)
            .ComErro(403)
            .ComErro(404)
            .SemCorpo(429)
            .RequireRateLimiting("escrita")
            .WithSummary("Exclui uma trilha privada")
            .WithDescription("Exige sessão e aceita apenas o dono da trilha. Retorna 204 quando a trilha é removida. Retorna 404 quando o recurso não existe.");

        return app;
    }
}

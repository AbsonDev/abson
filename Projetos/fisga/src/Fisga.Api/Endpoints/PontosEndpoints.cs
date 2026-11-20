using Fisga.Api.OpenApi;
using Fisga.Application.Pontos;
using Fisga.Domain.Pontos;

namespace Fisga.Api.Endpoints;

public static class PontosEndpoints
{
    public static IEndpointRouteBuilder MapPontosEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/pontos").WithTags(Tags.Pontos);

        grupo.MapGet("", async (
                HttpContext http,
                ServicoDePontos servico,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var lista = await servico.ListarAsync(usuarioId, ct);
                return Results.Ok(lista);
            })
            .RequireRateLimiting("leitura")
            .WithSummary("Lista os pontos privados")
            .WithDescription("Exige sessão e devolve apenas os pontos da conta, do mais recente para o mais antigo.");

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
            .RequireRateLimiting("escrita")
            .WithSummary("Salva um ponto privado")
            .WithDescription("Exige sessão. Cada conta pode manter até duzentos pontos privados.");

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
            .RequireRateLimiting("escrita")
            .WithSummary("Atualiza um ponto privado")
            .WithDescription("Exige sessão e aceita apenas o dono do ponto. Retorna 404 quando o ponto não existe.");

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
            .RequireRateLimiting("escrita")
            .WithSummary("Exclui um ponto privado")
            .WithDescription("Exige sessão e aceita apenas o dono do ponto. Retorna 204 quando o ponto é removido.");

        return app;
    }
}

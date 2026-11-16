using Microsoft.Extensions.Options;
using CardapioNoLucro.Api.Contracts;
using CardapioNoLucro.Application.Abstractions;
using CardapioNoLucro.Application.Catalogo;
using CardapioNoLucro.Application.Documentos;
using CardapioNoLucro.Application.Fichas;
using CardapioNoLucro.Infrastructure.Payments;

namespace CardapioNoLucro.Api.Endpoints;

public static class FichaEndpoints
{
    public static IEndpointRouteBuilder MapFichaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/canais", (ICatalogoDeCanais catalogo) =>
                Results.Ok(catalogo.ObterTodos().Select(canal => new CanalDto(
                    canal.Tipo.ToString(),
                    canal.Nome,
                    canal.Descricao,
                    canal.ComissaoPercentual,
                    canal.TaxaFixaPorPedido,
                    canal.Ativo))))
            .RequireRateLimiting("leitura");

        app.MapGet("/api/exemplo", (ICatalogoDeCanais catalogo) => Results.Ok(catalogo.ObterExemplo()))
            .RequireRateLimiting("leitura");

        var grupo = app.MapGroup("/api/fichas");

        grupo.MapPost("/preview", (EntradaDeFicha entrada, ServicoDeFichas servico) =>
                Results.Ok(servico.Simular(entrada)))
            .RequireRateLimiting("leitura");

        grupo.MapPost("", async (
                EntradaDeFicha entrada,
                ServicoDeFichas servico,
                IOptions<WebOptions> web,
                CancellationToken ct) =>
            {
                var salva = await servico.CriarAsync(entrada, ct);
                var url = $"{web.Value.Url.TrimEnd('/')}/f/{salva.ShareToken}";

                return Results.Created(
                    $"/api/fichas/{salva.ShareToken}",
                    new FichaSalvaResponse(salva.Id, salva.ShareToken, salva.EditToken, url));
            })
            .RequireRateLimiting("escrita");

        grupo.MapGet("/{shareToken}", async (
                string shareToken,
                ServicoDeFichas servico,
                CancellationToken ct) =>
            {
                var ficha = await servico.ObterPublicoAsync(shareToken, ct);
                return ficha is null
                    ? Results.NotFound(new ErroResponse("ficha_nao_encontrada", "Ficha não encontrada."))
                    : Results.Ok(ficha);
            })
            .RequireRateLimiting("leitura");

        grupo.MapPut("/{id:guid}", async (
                Guid id,
                HttpRequest request,
                EntradaDeFicha entrada,
                ServicoDeFichas servico,
                CancellationToken ct) =>
            {
                var editToken = request.Headers["X-Edit-Token"].ToString();
                var ficha = await servico.AtualizarAsync(id, editToken, entrada, ct);
                return Results.Ok(ficha);
            })
            .RequireRateLimiting("escrita");

        grupo.MapGet("/{shareToken}/pdf", async (
                string shareToken,
                ServicoDePdf pdf,
                IOptions<WebOptions> web,
                CancellationToken ct) =>
            {
                var url = $"{web.Value.Url.TrimEnd('/')}/f/{shareToken}";
                var arquivo = await pdf.GerarAsync(shareToken, url, ct);
                return Results.File(arquivo.Conteudo, "application/pdf", arquivo.NomeDoArquivo);
            })
            .RequireRateLimiting("pdf");

        return app;
    }
}

using Microsoft.Extensions.Options;
using Fisga.Api.Contracts;
using Fisga.Application.Abstractions;
using Fisga.Application.Catalogo;
using Fisga.Application.Documentos;
using Fisga.Application.Pescarias;
using Fisga.Infrastructure.Payments;

namespace Fisga.Api.Endpoints;

public static class PescariaEndpoints
{
    public static IEndpointRouteBuilder MapPescariaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalogo", (ICatalogoDePesca catalogo) => Results.Ok(catalogo.ObterCatalogo()))
            .RequireRateLimiting("leitura");

        var grupo = app.MapGroup("/api/pescarias");

        grupo.MapPost("", async (
                EntradaDePescaria entrada,
                HttpRequest request,
                ServicoDePescarias servico,
                IOptions<WebOptions> web,
                CancellationToken ct) =>
            {
                var diarioToken = request.Headers["X-Diary-Token"].ToString();
                var salva = await servico.CriarAsync(entrada, diarioToken, ct);
                var url = $"{web.Value.Url.TrimEnd('/')}/p/{salva.ShareToken}";

                return Results.Created(
                    $"/api/pescarias/{salva.ShareToken}",
                    new PescariaSalvaResponse(salva.Id, salva.ShareToken, salva.EditToken, url));
            })
            .RequireRateLimiting("escrita");

        grupo.MapGet("/{shareToken}", async (
                string shareToken,
                ServicoDePescarias servico,
                CancellationToken ct) =>
            {
                var pescaria = await servico.ObterPublicoAsync(shareToken, ct);
                return pescaria is null
                    ? Results.NotFound(new ErroResponse("pescaria_nao_encontrada", "Pescaria não encontrada."))
                    : Results.Ok(pescaria);
            })
            .RequireRateLimiting("leitura");

        grupo.MapPut("/{id:guid}", async (
                Guid id,
                HttpRequest request,
                EntradaDePescaria entrada,
                ServicoDePescarias servico,
                CancellationToken ct) =>
            {
                var editToken = request.Headers["X-Edit-Token"].ToString();
                var pescaria = await servico.AtualizarAsync(id, editToken, entrada, ct);
                return Results.Ok(pescaria);
            })
            .RequireRateLimiting("escrita");

        grupo.MapGet("/{shareToken}/pdf", async (
                string shareToken,
                ServicoDePdf pdf,
                IOptions<WebOptions> web,
                CancellationToken ct) =>
            {
                var url = $"{web.Value.Url.TrimEnd('/')}/p/{shareToken}";
                var arquivo = await pdf.GerarAsync(shareToken, url, ct);
                return Results.File(arquivo.Conteudo, "application/pdf", arquivo.NomeDoArquivo);
            })
            .RequireRateLimiting("pdf");

        app.MapGet("/api/diario", async (
                HttpRequest request,
                ServicoDoDiario diario,
                CancellationToken ct) =>
            {
                var token = request.Headers["X-Diary-Token"].ToString();
                var resultado = await diario.ObterAsync(token, ct);
                return Results.Ok(resultado);
            })
            .RequireRateLimiting("leitura");

        return app;
    }

    public static IEndpointRouteBuilder MapMidiaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/midia", async (
                HttpRequest request,
                IMidiaStorage midia,
                CancellationToken ct) =>
            {
                if (!request.HasFormContentType)
                {
                    return Results.BadRequest(new ErroResponse("midia_sem_formulario", "Envie a foto como formulário multipart."));
                }

                var formulario = await request.ReadFormAsync(ct);
                var arquivo = formulario.Files.GetFile("arquivo");

                if (arquivo is null || arquivo.Length == 0)
                {
                    return Results.BadRequest(new ErroResponse("midia_vazia", "Selecione uma foto para enviar."));
                }

                if (arquivo.Length > 5 * 1024 * 1024)
                {
                    return Results.BadRequest(new ErroResponse("midia_grande", "A foto deve ter no máximo 5 MB."));
                }

                var tipos = new[] { "image/jpeg", "image/png", "image/webp" };
                if (!tipos.Contains(arquivo.ContentType, StringComparer.OrdinalIgnoreCase))
                {
                    return Results.BadRequest(new ErroResponse("midia_tipo_invalido", "Envie uma imagem JPG, PNG ou WebP."));
                }

                await using var stream = arquivo.OpenReadStream();
                var url = await midia.SalvarAsync(stream, arquivo.ContentType, ct);
                return Results.Ok(new MidiaResponse(url));
            })
            .RequireRateLimiting("midia");

        return app;
    }
}

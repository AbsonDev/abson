using Microsoft.Extensions.Options;
using Precifica.Application.Abstractions;
using Precifica.Application.Calculos;
using Precifica.Application.Documentos;
using Precifica.Application.Pagamentos;
using Precifica.Api.Contracts;
using Precifica.Infrastructure.Payments;

namespace Precifica.Api.Endpoints;

public static class CalculoEndpoints
{
    public static IEndpointRouteBuilder MapCalculoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/calculos");

        grupo.MapPost("/preview", (EntradaDeCalculo entrada, ServicoDeCalculos servico) =>
                Results.Ok(servico.Simular(entrada)))
            .RequireRateLimiting("leitura");

        grupo.MapPost("", async (
                EntradaDeCalculo entrada,
                ServicoDeCalculos servico,
                IOptions<WebOptions> web,
                CancellationToken ct) =>
            {
                var salvo = await servico.CriarAsync(entrada, ct);
                var url = $"{web.Value.Url.TrimEnd('/')}/c/{salvo.ShareToken}";

                return Results.Created(
                    $"/api/calculos/{salvo.ShareToken}",
                    new CalculoSalvoResponse(salvo.Id, salvo.ShareToken, salvo.EditToken, url));
            })
            .RequireRateLimiting("escrita");

        grupo.MapGet("/{shareToken}", async (
                string shareToken,
                ServicoDeCalculos servico,
                CancellationToken ct) =>
            {
                var calculo = await servico.ObterPublicoAsync(shareToken, ct);
                return calculo is null
                    ? Results.NotFound(new ErroResponse("calculo_nao_encontrado", "Cálculo não encontrado."))
                    : Results.Ok(calculo);
            })
            .RequireRateLimiting("leitura");

        grupo.MapPut("/{id:guid}", async (
                Guid id,
                HttpRequest request,
                EntradaDeCalculo entrada,
                ServicoDeCalculos servico,
                CancellationToken ct) =>
            {
                var editToken = request.Headers["X-Edit-Token"].ToString();
                var calculo = await servico.AtualizarAsync(id, editToken, entrada, ct);
                return Results.Ok(calculo);
            })
            .RequireRateLimiting("escrita");

        grupo.MapGet("/{shareToken}/acesso", async (
                string shareToken,
                string? email,
                ServicoDeCalculos calculos,
                ServicoDeAcesso acesso,
                CancellationToken ct) =>
            {
                var calculo = await calculos.ObterPublicoAsync(shareToken, ct);
                if (calculo is null)
                {
                    return Results.NotFound(new ErroResponse("calculo_nao_encontrado", "Cálculo não encontrado."));
                }

                return Results.Ok(await acesso.ObterAsync(calculo.Id, email, ct));
            })
            .RequireRateLimiting("leitura");

        return app;
    }

    public static IEndpointRouteBuilder MapPdfEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/calculos");

        grupo.MapGet("/{shareToken}/pdf", async (
                string shareToken,
                ServicoDePdf pdf,
                IOptions<WebOptions> web,
                CancellationToken ct) =>
            {
                var url = $"{web.Value.Url.TrimEnd('/')}/c/{shareToken}";
                var arquivo = await pdf.GerarAsync(shareToken, premium: false, email: null, url, ct);
                return Results.File(arquivo.Conteudo, "application/pdf", arquivo.NomeDoArquivo);
            })
            .RequireRateLimiting("pdf");

        grupo.MapGet("/{shareToken}/pdf/premium", async (
                string shareToken,
                string? email,
                ServicoDePdf pdf,
                IOptions<WebOptions> web,
                CancellationToken ct) =>
            {
                var url = $"{web.Value.Url.TrimEnd('/')}/c/{shareToken}";
                var arquivo = await pdf.GerarAsync(shareToken, premium: true, email, url, ct);
                return Results.File(arquivo.Conteudo, "application/pdf", arquivo.NomeDoArquivo);
            })
            .RequireRateLimiting("pdf");

        return app;
    }
}

using Fisga.Api.Contracts;
using Fisga.Api.OpenApi;
using Fisga.Application.Vision;

namespace Fisga.Api.Endpoints;

public static class VisionEndpoints
{
    public static IEndpointRouteBuilder MapVisionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/especies/sugerir", async (
                HttpContext http,
                IReconhecedorDeEspecie reconhecedor,
                CancellationToken ct) =>
            {
                http.ExigirUsuarioId();

                if (!http.Request.HasFormContentType)
                {
                    return Results.BadRequest(
                        new ErroResponse(CodigosDeErro.SugestaoSemFormulario, "Envie a foto como formulário multipart."));
                }

                var formulario = await http.Request.ReadFormAsync(ct);
                var arquivo = formulario.Files.GetFile("arquivo");
                if (arquivo is null || arquivo.Length == 0)
                {
                    return Results.BadRequest(
                        new ErroResponse(CodigosDeErro.SugestaoSemFoto, "Selecione uma foto para analisar."));
                }

                if (arquivo.Length > 5 * 1024 * 1024)
                {
                    return Results.BadRequest(
                        new ErroResponse(CodigosDeErro.SugestaoFotoGrande, "A foto deve ter no máximo 5 MB."));
                }

                var tipos = new[] { "image/jpeg", "image/png", "image/webp" };
                if (!tipos.Contains(arquivo.ContentType, StringComparer.OrdinalIgnoreCase))
                {
                    return Results.BadRequest(
                        new ErroResponse(CodigosDeErro.SugestaoTipoInvalido, "Envie uma imagem JPG, PNG ou WebP."));
                }

                await using var fluxo = arquivo.OpenReadStream();
                var resultado = await reconhecedor.SugerirAsync(fluxo, arquivo.ContentType, ct);
                return Results.Ok(resultado);
            })
            .Produces<SugestaoDeEspecieResultado>(200)
            .Accepts<FormularioDeFoto>("multipart/form-data")
            .ComErro(400)
            .ComErro(401)
            .ComErro(403)
            .SemCorpo(429)
            .ComErro(500)
            .RequireRateLimiting("midia")
            .WithTags(Tags.Especies)
            .WithSummary("Sugere a espécie a partir da foto enviada")
            .WithDescription("Exige sessão e recebe a foto como formulário multipart no campo arquivo. Retorna 200 com a sugestão de espécie. Retorna 400 com o motivo quando o formulário está ausente, o arquivo está ausente, a foto passa de 5 MB ou o tipo não é aceito (JPG, PNG ou WebP).");

        return app;
    }
}

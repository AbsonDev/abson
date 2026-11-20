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
                        new ErroResponse("sugestao_sem_formulario", "Envie a foto como formulário multipart."));
                }

                var formulario = await http.Request.ReadFormAsync(ct);
                var arquivo = formulario.Files.GetFile("arquivo");
                if (arquivo is null || arquivo.Length == 0)
                {
                    return Results.BadRequest(
                        new ErroResponse("sugestao_sem_foto", "Selecione uma foto para analisar."));
                }

                if (arquivo.Length > 5 * 1024 * 1024)
                {
                    return Results.BadRequest(
                        new ErroResponse("sugestao_foto_grande", "A foto deve ter no máximo 5 MB."));
                }

                var tipos = new[] { "image/jpeg", "image/png", "image/webp" };
                if (!tipos.Contains(arquivo.ContentType, StringComparer.OrdinalIgnoreCase))
                {
                    return Results.BadRequest(
                        new ErroResponse("sugestao_tipo_invalido", "Envie uma imagem JPG, PNG ou WebP."));
                }

                await using var fluxo = arquivo.OpenReadStream();
                var resultado = await reconhecedor.SugerirAsync(fluxo, arquivo.ContentType, ct);
                return Results.Ok(resultado);
            })
            .RequireRateLimiting("midia")
            .WithTags(Tags.Especies)
            .WithSummary("Sugere a espécie a partir da foto enviada")
            .WithDescription("Exige sessão e recebe a foto como formulário multipart no campo arquivo, com até 5 MB em JPG, PNG ou WebP. Retorna 400 com o motivo quando o formulário, o arquivo ou o tipo não são aceitos.");

        return app;
    }
}

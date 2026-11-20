using Microsoft.Extensions.Options;
using Fisga.Api.Caching;
using Fisga.Api.Contracts;
using Fisga.Api.Observabilidade;
using Fisga.Api.OpenApi;
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
            .AllowAnonymous()
            .RequireRateLimiting("leitura")
            .WithMetadata(CachePolicy.Public(86400))
            .WithTags(Tags.Catalogo)
            .WithSummary("Consulta o catálogo de pesca")
            .WithDescription("Retorna as espécies, iscas, ambientes e demais opções usadas no registro, além de um exemplo de pescaria. Aberto sem sessão e mantido em cache por um dia.");

        var grupo = app.MapGroup("/api/pescarias").WithTags(Tags.Pescarias);

        grupo.MapPost("", async (
                EntradaDePescaria entrada,
                HttpContext http,
                ServicoDePescarias servico,
                IOptions<WebOptions> web,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var salva = await servico.CriarAsync(entrada, usuarioId, ct);
                Metricas.PescariaCriada(entrada.Ambiente, entrada.Capturas);
                var url = $"{web.Value.Url.TrimEnd('/')}/p/{salva.ShareToken}";

                return Results.Created(
                    $"/api/pescarias/{salva.ShareToken}",
                    new PescariaSalvaResponse(salva.Id, salva.ShareToken, url));
            })
            .RequireRateLimiting("escrita")
            .WithSummary("Registra uma pescaria")
            .WithDescription("Exige sessão. Retorna o identificador, o token público e o endereço da página de compartilhamento.");

        grupo.MapGet("/{shareToken}", async (
                string shareToken,
                HttpContext http,
                ServicoDePescarias servico,
                CancellationToken ct) =>
            {
                var pescaria = await servico.ObterPublicoAsync(shareToken, http.UsuarioId(), ct);
                return pescaria is null
                    ? Results.NotFound(new ErroResponse("pescaria_nao_encontrada", "Pescaria não encontrada."))
                    : Results.Ok(pescaria);
            })
            .AllowAnonymous()
            .RequireRateLimiting("leitura")
            .WithMetadata(CachePolicy.Private(30))
            .WithSummary("Abre uma pescaria compartilhada")
            .WithDescription("Funciona sem sessão e retorna 404 quando o token não existe. O dono recebe a pescaria com permissão de edição.");

        grupo.MapGet("/{shareToken}/permissao", async (
                string shareToken,
                HttpContext http,
                ServicoDePescarias servico,
                CancellationToken ct) =>
            {
                var pescaria = await servico.ObterPublicoAsync(shareToken, http.UsuarioId(), ct);
                return pescaria is null
                    ? Results.NotFound(new ErroResponse("pescaria_nao_encontrada", "Pescaria não encontrada."))
                    : Results.Ok(new PermissaoResponse(pescaria.Id, pescaria.PodeEditar));
            })
            .AllowAnonymous()
            .RequireRateLimiting("leitura")
            .WithSummary("Consulta a permissão de edição de uma pescaria")
            .WithDescription("Funciona sem sessão e retorna 404 quando o token não existe. Informa se o visitante atual pode editar a pescaria.");

        grupo.MapGet("/{id:guid}/editar", async (
                Guid id,
                HttpContext http,
                ServicoDePescarias servico,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var pescaria = await servico.ObterDoUsuarioAsync(id, usuarioId, ct);
                return Results.Ok(pescaria);
            })
            .RequireRateLimiting("leitura")
            .WithSummary("Carrega uma pescaria para edição")
            .WithDescription("Exige sessão e aceita apenas o dono da pescaria. Retorna 404 quando a pescaria não existe ou pertence a outra conta.");

        grupo.MapPut("/{id:guid}", async (
                Guid id,
                HttpContext http,
                EntradaDePescaria entrada,
                ServicoDePescarias servico,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var pescaria = await servico.AtualizarAsync(id, usuarioId, entrada, ct);
                Metricas.PescariaAtualizada(entrada.Ambiente);
                return Results.Ok(pescaria);
            })
            .RequireRateLimiting("escrita")
            .WithSummary("Atualiza uma pescaria")
            .WithDescription("Exige sessão e aceita apenas o dono da pescaria. Retorna 404 quando a pescaria não existe ou pertence a outra conta.");

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
            .AllowAnonymous()
            .RequireRateLimiting("pdf")
            .WithMetadata(CachePolicy.Private(300))
            .WithSummary("Exporta uma pescaria em PDF")
            .WithDescription("Funciona sem sessão e retorna o arquivo pronto para baixar. Retorna 404 quando o token não existe.");

        app.MapGet("/api/diario", async (
                HttpContext http,
                ServicoDoDiario diario,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var resultado = await diario.ObterAsync(usuarioId, ct);
                return Results.Ok(resultado);
            })
            .RequireRateLimiting("leitura")
            .WithTags(Tags.Diario)
            .WithSummary("Consulta o diário do pescador")
            .WithDescription("Exige sessão e reúne as pescarias da conta em ordem de data, com o número de capturas de cada uma.");

        return app;
    }

    public static IEndpointRouteBuilder MapMidiaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/midia", async (
                HttpContext http,
                IMidiaStorage midia,
                CancellationToken ct) =>
            {
                http.ExigirUsuarioId();

                if (!http.Request.HasFormContentType)
                {
                    return Results.BadRequest(new ErroResponse("midia_sem_formulario", "Envie a foto como formulário multipart."));
                }

                var formulario = await http.Request.ReadFormAsync(ct);
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
                Metricas.MidiaEnviada(arquivo.ContentType, arquivo.Length);
                return Results.Ok(new MidiaResponse(url));
            })
            .RequireRateLimiting("midia")
            .WithTags(Tags.Midia)
            .WithSummary("Envia uma foto")
            .WithDescription("Exige sessão e recebe a imagem como formulário multipart, com até 5 MB em JPG, PNG ou WebP. Retorna o endereço público da foto.");

        return app;
    }
}

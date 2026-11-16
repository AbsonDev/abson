using System.Text.Json;
using Microsoft.Extensions.Options;
using Precifica.Application.Pagamentos;
using Precifica.Api.Contracts;
using Precifica.Application.Abstractions;
using Precifica.Application.Calculos;
using Precifica.Application.Catalogo;
using Precifica.Infrastructure.Payments;

namespace Precifica.Api.Endpoints;

public static class PagamentoEndpoints
{
    public static IEndpointRouteBuilder MapPagamentoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/nichos", (ICatalogoDeNichos catalogo) => Results.Ok(catalogo.ObterTodos()))
            .RequireRateLimiting("leitura");

        app.MapGet("/api/planos", (IOptions<MonetizacaoOptions> monetizacao) =>
            {
                var gratuito = !monetizacao.Value.Ativa;

                return Results.Ok(new PlanosResponse(
                    gratuito ? 0 : CatalogoDePlanos.PdfAvulso.Reais,
                    gratuito ? 0 : CatalogoDePlanos.ProMensal.Reais,
                    CatalogoDePlanos.NomeDoPlanoPro,
                    gratuito));
            })
            .RequireRateLimiting("leitura");

        app.MapPost("/api/calculos/{shareToken}/comprar", async (
                string shareToken,
                CheckoutRequest request,
                ServicoDePagamentos pagamentos,
                IOptions<WebOptions> web,
                IConfiguration configuracao,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.Email))
                {
                    return Results.BadRequest(new ErroResponse("email_obrigatorio", "Informe um e-mail para receber o acesso."));
                }

                var urlRetorno = $"{web.Value.Url.TrimEnd('/')}/c/{shareToken}?pagamento=sucesso";
                var checkout = await pagamentos.CriarCheckoutDePdfAsync(
                    shareToken,
                    request.Email,
                    urlRetorno,
                    UrlDoWebhook(configuracao),
                    ct);

                return Results.Ok(new CheckoutResponse(checkout.Url, checkout.Id));
            })
            .RequireRateLimiting("escrita");

        app.MapPost("/api/assinaturas", async (
                CheckoutRequest request,
                ServicoDePagamentos pagamentos,
                IOptions<WebOptions> web,
                IConfiguration configuracao,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.Email))
                {
                    return Results.BadRequest(new ErroResponse("email_obrigatorio", "Informe seu e-mail para assinar."));
                }

                var urlRetorno = $"{web.Value.Url.TrimEnd('/')}/precos?assinatura=sucesso";
                var checkout = await pagamentos.CriarCheckoutDeAssinaturaAsync(
                    request.Email,
                    urlRetorno,
                    UrlDoWebhook(configuracao),
                    ct);

                return Results.Ok(new CheckoutResponse(checkout.Url, checkout.Id));
            })
            .RequireRateLimiting("escrita");

        app.MapPost("/api/webhooks/mercadopago", async (
                HttpRequest request,
                IGatewayDePagamento gateway,
                ServicoDePagamentos pagamentos,
                ILoggerFactory loggerFactory,
                CancellationToken ct) =>
            {
                var logger = loggerFactory.CreateLogger("WebhookMercadoPago");

                var assinatura = request.Headers["x-signature"].ToString();
                var requestId = request.Headers["x-request-id"].ToString();
                var (tipo, dataId) = await ExtrairNotificacaoAsync(request);

                if (string.IsNullOrWhiteSpace(dataId))
                {
                    return Results.Ok();
                }

                if (!gateway.ValidarWebhook(assinatura, requestId, dataId))
                {
                    logger.LogWarning("Webhook com assinatura inválida para {Tipo} {Id}", tipo, dataId);
                    return Results.Unauthorized();
                }

                var processado = tipo switch
                {
                    "payment" => await pagamentos.ProcessarNotificacaoDePagamentoAsync(dataId, ct),
                    "subscription_preapproval" or "preapproval" =>
                        await pagamentos.ProcessarNotificacaoDeAssinaturaAsync(dataId, ct),
                    _ => false
                };

                logger.LogInformation(
                    "Webhook {Tipo} {Id}: {Resultado}",
                    tipo,
                    dataId,
                    processado ? "processado" : "ignorado");

                return Results.Ok();
            })
            .AllowAnonymous();

        return app;
    }

    public static IEndpointRouteBuilder MapDevEndpoints(this IEndpointRouteBuilder app, bool habilitado)
    {
        if (!habilitado)
        {
            return app;
        }

        app.MapGet("/api/dev/checkout/{id:guid}", async (
                Guid id,
                ServicoDePagamentos pagamentos,
                IOptions<WebOptions> web,
                CancellationToken ct) =>
            {
                var simulacao = await pagamentos.ObterSimulacaoAsync(id, ct);
                if (simulacao is null)
                {
                    return Results.NotFound(new ErroResponse("checkout_nao_encontrado", "Checkout não encontrado."));
                }

                return Results.Ok(new SimulacaoResponse(
                    simulacao.Tipo,
                    simulacao.Status,
                    simulacao.Valor,
                    simulacao.Email,
                    UrlRetorno(simulacao, web.Value.Url),
                    simulacao.ShareToken));
            })
            .AllowAnonymous();

        app.MapPost("/api/dev/checkout/{id:guid}/confirmar", async (
                Guid id,
                ServicoDePagamentos pagamentos,
                IOptions<WebOptions> web,
                CancellationToken ct) =>
            {
                var simulacao = await pagamentos.ConfirmarSimuladoAsync(id, ct);
                return Results.Ok(new SimulacaoConfirmadaResponse(
                    simulacao,
                    UrlRetorno(simulacao, web.Value.Url)));
            })
            .AllowAnonymous();

        return app;
    }

    private static string UrlDoWebhook(IConfiguration configuracao)
    {
        var baseUrl = configuracao["Api:PublicUrl"]?.TrimEnd('/')
                      ?? "http://localhost:5180";
        return $"{baseUrl}/api/webhooks/mercadopago";
    }

    private static string UrlRetorno(SimulacaoDto simulacao, string webUrl) =>
        simulacao.Tipo == "pedido" && simulacao.ShareToken is not null
            ? $"{webUrl.TrimEnd('/')}/c/{simulacao.ShareToken}?pagamento=sucesso"
            : $"{webUrl.TrimEnd('/')}/precos?assinatura=sucesso";

    private static async Task<(string Tipo, string? Id)> ExtrairNotificacaoAsync(HttpRequest request)
    {
        var tipo = request.Query["type"].ToString();
        if (string.IsNullOrWhiteSpace(tipo))
        {
            tipo = request.Query["topic"].ToString();
        }

        var dataId = request.Query["data.id"].ToString();
        if (string.IsNullOrWhiteSpace(dataId))
        {
            dataId = request.Query["id"].ToString();
        }

        if (!string.IsNullOrWhiteSpace(tipo) && !string.IsNullOrWhiteSpace(dataId))
        {
            return (tipo, dataId);
        }

        try
        {
            using var documento = await JsonDocument.ParseAsync(request.Body);
            var raiz = documento.RootElement;

            if (string.IsNullOrWhiteSpace(tipo) && raiz.TryGetProperty("type", out var tipoJson))
            {
                tipo = tipoJson.GetString() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(dataId)
                && raiz.TryGetProperty("data", out var data)
                && data.TryGetProperty("id", out var idJson))
            {
                dataId = idJson.ValueKind == JsonValueKind.Number
                    ? idJson.GetInt64().ToString()
                    : idJson.GetString();
            }
        }
        catch (JsonException)
        {
            // Payload inválido: o endpoint responde 200 para não gerar retentativas infinitas.
        }

        return (tipo, dataId);
    }
}

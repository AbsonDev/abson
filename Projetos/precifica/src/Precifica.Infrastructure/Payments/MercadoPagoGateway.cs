using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Precifica.Application.Abstractions;
using Precifica.Domain.Common;
using Precifica.Domain.Pagamentos;

namespace Precifica.Infrastructure.Payments;

/// <summary>
/// Adaptador do Mercado Pago (Checkout Pro e preapproval). O domínio não vê
/// nenhum DTO do provedor: os formatos ficam confinados nesta classe.
/// </summary>
public sealed class MercadoPagoGateway(
    HttpClient http,
    IOptions<MercadoPagoOptions> options,
    ILogger<MercadoPagoGateway> logger) : IGatewayDePagamento
{
    private readonly MercadoPagoOptions _opcoes = options.Value;

    public async Task<CheckoutDePagamento> CriarCheckoutDePedidoAsync(
        Pedido pedido,
        string descricao,
        string urlRetorno,
        string urlNotificacao,
        CancellationToken ct = default)
    {
        var corpo = new
        {
            items = new[]
            {
                new
                {
                    title = descricao,
                    quantity = 1,
                    unit_price = pedido.Valor.Reais,
                    currency_id = "BRL"
                }
            },
            payer = new { email = pedido.Email },
            external_reference = pedido.Id.ToString(),
            notification_url = urlNotificacao,
            back_urls = new
            {
                success = urlRetorno,
                failure = urlRetorno,
                pending = urlRetorno
            },
            auto_return = "approved",
            statement_descriptor = "PRECIFICA"
        };

        using var resposta = await EnviarAsync(HttpMethod.Post, "/checkout/preferences", corpo, ct);
        var raiz = resposta.RootElement;

        var referencia = LerTexto(raiz, "id")
                         ?? throw new IntegracaoIndisponivelException("Mercado Pago não retornou o id da preferência.");
        var url = LerTexto(raiz, "init_point") ?? LerTexto(raiz, "sandbox_init_point")
                  ?? throw new IntegracaoIndisponivelException("Mercado Pago não retornou a URL de checkout.");

        return new CheckoutDePagamento(referencia, url);
    }

    public async Task<CheckoutDePagamento> CriarCheckoutDeAssinaturaAsync(
        Assinatura assinatura,
        string urlRetorno,
        string urlNotificacao,
        CancellationToken ct = default)
    {
        var corpo = new
        {
            reason = "Precifica Pro",
            external_reference = assinatura.Id.ToString(),
            payer_email = assinatura.Email,
            back_url = urlRetorno,
            notification_url = urlNotificacao,
            status = "pending",
            auto_recurring = new
            {
                frequency = 1,
                frequency_type = "months",
                transaction_amount = assinatura.ValorMensal.Reais,
                currency_id = "BRL"
            }
        };

        using var resposta = await EnviarAsync(HttpMethod.Post, "/preapproval", corpo, ct);
        var raiz = resposta.RootElement;

        var referencia = LerTexto(raiz, "id")
                         ?? throw new IntegracaoIndisponivelException("Mercado Pago não retornou o id da assinatura.");
        var url = LerTexto(raiz, "init_point") ?? LerTexto(raiz, "sandbox_init_point")
                  ?? throw new IntegracaoIndisponivelException("Mercado Pago não retornou a URL da assinatura.");

        return new CheckoutDePagamento(referencia, url);
    }

    public async Task<PagamentoNoGateway?> ObterPagamentoAsync(string pagamentoId, CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(
            HttpMethod.Get,
            $"/v1/payments/{Uri.EscapeDataString(pagamentoId)}",
            null,
            ct);

        var raiz = resposta.RootElement;
        var status = LerTexto(raiz, "status");
        if (status is null)
        {
            return null;
        }

        var valor = LerDecimal(raiz, "transaction_amount") ?? 0m;

        return new PagamentoNoGateway(
            LerTexto(raiz, "id") ?? pagamentoId,
            status,
            LerTexto(raiz, "external_reference"),
            Dinheiro.DeReais(valor).Centavos);
    }

    public async Task<AssinaturaNoGateway?> ObterAssinaturaAsync(
        string assinaturaId,
        CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(
            HttpMethod.Get,
            $"/preapproval/{Uri.EscapeDataString(assinaturaId)}",
            null,
            ct);

        var raiz = resposta.RootElement;
        var status = LerTexto(raiz, "status");
        if (status is null)
        {
            return null;
        }

        return new AssinaturaNoGateway(
            LerTexto(raiz, "id") ?? assinaturaId,
            status,
            LerTexto(raiz, "external_reference"));
    }

    public bool ValidarWebhook(string? assinatura, string? requestId, string? dataId)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.WebhookSecret))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(assinatura) || string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(dataId))
        {
            return false;
        }

        var partes = assinatura
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(parte => parte.Split('=', 2))
            .Where(parte => parte.Length == 2)
            .ToDictionary(parte => parte[0], parte => parte[1], StringComparer.OrdinalIgnoreCase);

        if (!partes.TryGetValue("ts", out var timestamp) || !partes.TryGetValue("v1", out var assinaturaRecebida))
        {
            return false;
        }

        var manifesto = $"id:{dataId};request-id:{requestId};ts:{timestamp};";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_opcoes.WebhookSecret));
        var esperado = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(manifesto))).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(esperado),
            Encoding.UTF8.GetBytes(assinaturaRecebida.ToLowerInvariant()));
    }

    private async Task<JsonDocument> EnviarAsync(
        HttpMethod metodo,
        string caminho,
        object? corpo,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.AccessToken))
        {
            throw new IntegracaoIndisponivelException(
                "Pagamento online não configurado. Defina MercadoPago:AccessToken ou use Pagamentos:Modo=Simulado.");
        }

        using var requisicao = new HttpRequestMessage(metodo, caminho);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opcoes.AccessToken);
        requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (corpo is not null)
        {
            requisicao.Content = JsonContent.Create(corpo);
        }

        using var resposta = await http.SendAsync(requisicao, ct);
        var conteudo = await resposta.Content.ReadAsStringAsync(ct);

        if (!resposta.IsSuccessStatusCode)
        {
            logger.LogError(
                "Mercado Pago respondeu {Status} em {Metodo} {Caminho}: {Corpo}",
                (int)resposta.StatusCode,
                metodo,
                caminho,
                conteudo);

            throw new IntegracaoIndisponivelException(
                "O Mercado Pago não conseguiu iniciar o pagamento agora. Tente novamente em instantes.");
        }

        return JsonDocument.Parse(conteudo);
    }

    private static string? LerTexto(JsonElement elemento, string propriedade)
    {
        if (!elemento.TryGetProperty(propriedade, out var valor))
        {
            return null;
        }

        return valor.ValueKind switch
        {
            JsonValueKind.String => valor.GetString(),
            JsonValueKind.Number => valor.GetRawText(),
            _ => null
        };
    }

    private static decimal? LerDecimal(JsonElement elemento, string propriedade)
    {
        if (!elemento.TryGetProperty(propriedade, out var valor))
        {
            return null;
        }

        return valor.ValueKind == JsonValueKind.Number
            ? valor.GetDecimal()
            : decimal.TryParse(valor.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var numero)
                ? numero
                : null;
    }
}

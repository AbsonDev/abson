using Precifica.Domain.Pagamentos;

namespace Precifica.Application.Abstractions;

/// <summary>
/// Porta de saída para o provedor de pagamentos. O domínio e a aplicação não
/// conhecem o formato do Mercado Pago: quem traduz é a infraestrutura.
/// </summary>
public interface IGatewayDePagamento
{
    Task<CheckoutDePagamento> CriarCheckoutDePedidoAsync(
        Pedido pedido,
        string descricao,
        string urlRetorno,
        string urlNotificacao,
        CancellationToken ct = default);

    Task<CheckoutDePagamento> CriarCheckoutDeAssinaturaAsync(
        Assinatura assinatura,
        string urlRetorno,
        string urlNotificacao,
        CancellationToken ct = default);

    Task<PagamentoNoGateway?> ObterPagamentoAsync(string pagamentoId, CancellationToken ct = default);

    Task<AssinaturaNoGateway?> ObterAssinaturaAsync(string assinaturaId, CancellationToken ct = default);

    /// <summary>Valida a assinatura HMAC do webhook, quando o provedor oferece suporte.</summary>
    bool ValidarWebhook(string? assinatura, string? requestId, string? dataId);
}

public sealed record CheckoutDePagamento(string Referencia, string Url);

public sealed record PagamentoNoGateway(
    string Id,
    string Status,
    string? ReferenciaExterna,
    long ValorEmCentavos);

public sealed record AssinaturaNoGateway(
    string Id,
    string Status,
    string? ReferenciaExterna);

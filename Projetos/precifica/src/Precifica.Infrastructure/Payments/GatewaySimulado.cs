using Microsoft.Extensions.Options;
using Precifica.Application.Abstractions;
using Precifica.Domain.Pagamentos;

namespace Precifica.Infrastructure.Payments;

/// <summary>
/// Gateway de desenvolvimento: devolve uma URL local de checkout e permite
/// confirmar o pagamento sem credenciais do Mercado Pago.
/// </summary>
public sealed class GatewaySimulado(IOptions<WebOptions> web) : IGatewayDePagamento
{
    private string UrlCheckout(Guid id) =>
        $"{web.Value.Url.TrimEnd('/')}/checkout/simulado/{id}";

    public Task<CheckoutDePagamento> CriarCheckoutDePedidoAsync(
        Pedido pedido,
        string descricao,
        string urlRetorno,
        string urlNotificacao,
        CancellationToken ct = default) =>
        Task.FromResult(new CheckoutDePagamento(pedido.Id.ToString(), UrlCheckout(pedido.Id)));

    public Task<CheckoutDePagamento> CriarCheckoutDeAssinaturaAsync(
        Assinatura assinatura,
        string urlRetorno,
        string urlNotificacao,
        CancellationToken ct = default) =>
        Task.FromResult(new CheckoutDePagamento(assinatura.Id.ToString(), UrlCheckout(assinatura.Id)));

    public Task<PagamentoNoGateway?> ObterPagamentoAsync(string pagamentoId, CancellationToken ct = default) =>
        throw new IntegracaoIndisponivelException("O modo simulado não consulta pagamentos externos.");

    public Task<AssinaturaNoGateway?> ObterAssinaturaAsync(string assinaturaId, CancellationToken ct = default) =>
        throw new IntegracaoIndisponivelException("O modo simulado não consulta assinaturas externas.");

    public bool ValidarWebhook(string? assinatura, string? requestId, string? dataId) => true;
}

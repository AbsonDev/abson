using Microsoft.Extensions.Options;
using Precifica.Application.Abstractions;
using Precifica.Application.Calculos;
using Precifica.Application.Catalogo;
using Precifica.Domain.Common;
using Precifica.Domain.Pagamentos;

namespace Precifica.Application.Pagamentos;

public sealed record CheckoutDto(string Url, Guid Id);

public sealed record SimulacaoDto(
    string Tipo,
    string Status,
    decimal Valor,
    string Email,
    Guid? CalculoId,
    string? ShareToken);

public sealed class ServicoDePagamentos(
    ICalculoRepository calculos,
    IPedidoRepository pedidos,
    IAssinaturaRepository assinaturas,
    IGatewayDePagamento gateway,
    IRelogio relogio,
    IDomainEventDispatcher eventos,
    IOptions<MonetizacaoOptions> monetizacao)
{
    public async Task<CheckoutDto> CriarCheckoutDePdfAsync(
        string shareToken,
        string email,
        string urlRetorno,
        string urlNotificacao,
        CancellationToken ct = default)
    {
        GarantirMonetizacaoAtiva();

        var calculo = await calculos.ObterPorShareTokenAsync(shareToken, ct)
                      ?? throw new RecursoNaoEncontradoException("calculo_nao_encontrado", "Cálculo não encontrado.");

        var pedido = Pedido.CriarPdfAvulso(calculo.Id, email, CatalogoDePlanos.PdfAvulso, relogio.Agora);
        var checkout = await gateway.CriarCheckoutDePedidoAsync(
            pedido,
            $"PDF sem marca d'água — {calculo.Titulo}",
            urlRetorno,
            urlNotificacao,
            ct);

        pedido.VincularReferencia(checkout.Referencia);
        await pedidos.AdicionarAsync(pedido, ct);

        return new CheckoutDto(checkout.Url, pedido.Id);
    }

    public async Task<CheckoutDto> CriarCheckoutDeAssinaturaAsync(
        string email,
        string urlRetorno,
        string urlNotificacao,
        CancellationToken ct = default)
    {
        GarantirMonetizacaoAtiva();

        var assinatura = Assinatura.Criar(email, CatalogoDePlanos.ProMensal, relogio.Agora);
        var checkout = await gateway.CriarCheckoutDeAssinaturaAsync(
            assinatura,
            urlRetorno,
            urlNotificacao,
            ct);

        assinatura.VincularReferencia(checkout.Referencia);
        await assinaturas.AdicionarAsync(assinatura, ct);

        return new CheckoutDto(checkout.Url, assinatura.Id);
    }

    /// <summary>Retorna true quando a notificação foi reconhecida (mesmo se já processada).</summary>
    public async Task<bool> ProcessarNotificacaoDePagamentoAsync(string pagamentoId, CancellationToken ct = default)
    {
        var pagamento = await gateway.ObterPagamentoAsync(pagamentoId, ct);
        if (pagamento is null || !StatusAprovado(pagamento.Status))
        {
            return false;
        }

        if (!Guid.TryParse(pagamento.ReferenciaExterna, out var pedidoId))
        {
            return false;
        }

        var pedido = await pedidos.ObterPorIdAsync(pedidoId, ct);
        if (pedido is null)
        {
            return false;
        }

        var confirmou = pedido.ConfirmarPagamento(
            pagamento.Id,
            new Dinheiro(pagamento.ValorEmCentavos),
            relogio.Agora);

        if (confirmou)
        {
            await pedidos.SalvarAsync(pedido, ct);
            await PublicarEventosAsync(pedido, ct);
        }

        return true;
    }

    public async Task<bool> ProcessarNotificacaoDeAssinaturaAsync(
        string referenciaGateway,
        CancellationToken ct = default)
    {
        var assinaturaGateway = await gateway.ObterAssinaturaAsync(referenciaGateway, ct);
        if (assinaturaGateway is null)
        {
            return false;
        }

        var assinatura = await LocalizarAssinaturaAsync(assinaturaGateway, ct);
        if (assinatura is null || assinatura.Status == StatusDaAssinatura.Cancelada)
        {
            return false;
        }

        switch (assinaturaGateway.Status.Trim().ToLowerInvariant())
        {
            case "authorized":
                assinatura.Ativar(assinaturaGateway.Id, relogio.Agora);
                break;
            case "paused":
                assinatura.MarcarInadimplente();
                break;
            case "cancelled":
                assinatura.Cancelar(relogio.Agora);
                break;
            default:
                return false;
        }

        await assinaturas.SalvarAsync(assinatura, ct);
        await PublicarEventosAsync(assinatura, ct);
        return true;
    }

    /// <summary>Confirmação usada apenas pelo checkout simulado de desenvolvimento.</summary>
    public async Task<SimulacaoDto> ConfirmarSimuladoAsync(Guid id, CancellationToken ct = default)
    {
        var pedido = await pedidos.ObterPorIdAsync(id, ct);
        if (pedido is not null)
        {
            pedido.ConfirmarPagamento($"simulado-{id}", pedido.Valor, relogio.Agora);
            await pedidos.SalvarAsync(pedido, ct);
            await PublicarEventosAsync(pedido, ct);

            var calculo = await calculos.ObterPorIdAsync(pedido.CalculoId, ct);
            return new SimulacaoDto(
                "pedido",
                pedido.Status.ToString(),
                pedido.Valor.Reais,
                pedido.Email,
                pedido.CalculoId,
                calculo?.ShareToken);
        }

        var assinatura = await assinaturas.ObterPorIdAsync(id, ct)
                         ?? throw new RecursoNaoEncontradoException("checkout_nao_encontrado", "Checkout não encontrado.");

        if (assinatura.Status != StatusDaAssinatura.Ativa)
        {
            assinatura.Ativar($"simulado-{id}", relogio.Agora);
            await assinaturas.SalvarAsync(assinatura, ct);
            await PublicarEventosAsync(assinatura, ct);
        }

        return new SimulacaoDto(
            "assinatura",
            assinatura.Status.ToString(),
            assinatura.ValorMensal.Reais,
            assinatura.Email,
            null,
            null);
    }

    public async Task<SimulacaoDto?> ObterSimulacaoAsync(Guid id, CancellationToken ct = default)
    {
        var pedido = await pedidos.ObterPorIdAsync(id, ct);
        if (pedido is not null)
        {
            var calculo = await calculos.ObterPorIdAsync(pedido.CalculoId, ct);
            return new SimulacaoDto(
                "pedido",
                pedido.Status.ToString(),
                pedido.Valor.Reais,
                pedido.Email,
                pedido.CalculoId,
                calculo?.ShareToken);
        }

        var assinatura = await assinaturas.ObterPorIdAsync(id, ct);
        if (assinatura is null)
        {
            return null;
        }

        return new SimulacaoDto(
            "assinatura",
            assinatura.Status.ToString(),
            assinatura.ValorMensal.Reais,
            assinatura.Email,
            null,
            null);
    }

    private async Task<Assinatura?> LocalizarAssinaturaAsync(
        AssinaturaNoGateway assinaturaGateway,
        CancellationToken ct)
    {
        if (Guid.TryParse(assinaturaGateway.ReferenciaExterna, out var assinaturaId))
        {
            var porId = await assinaturas.ObterPorIdAsync(assinaturaId, ct);
            if (porId is not null)
            {
                return porId;
            }
        }

        return await assinaturas.ObterPorReferenciaGatewayAsync(assinaturaGateway.Id, ct);
    }

    private async Task PublicarEventosAsync(AggregateRoot agregado, CancellationToken ct)
    {
        if (agregado.Eventos.Count == 0)
        {
            return;
        }

        await eventos.PublicarAsync(agregado.Eventos, ct);
        agregado.LimparEventos();
    }

    private static bool StatusAprovado(string status) =>
        string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase);

    private void GarantirMonetizacaoAtiva()
    {
        if (!monetizacao.Value.Ativa)
        {
            throw new IntegracaoIndisponivelException(
                "O Precifica está gratuito por enquanto. Não é necessário comprar nada.");
        }
    }
}

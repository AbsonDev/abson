using Precifica.Domain.Pagamentos;

namespace Precifica.Application.Abstractions;

public interface IPedidoRepository
{
    Task AdicionarAsync(Pedido pedido, CancellationToken ct = default);

    Task<Pedido?> ObterPorIdAsync(Guid id, CancellationToken ct = default);

    Task<bool> ExistePagoParaCalculoAsync(Guid calculoId, CancellationToken ct = default);

    Task SalvarAsync(Pedido pedido, CancellationToken ct = default);
}

public interface IAssinaturaRepository
{
    Task AdicionarAsync(Assinatura assinatura, CancellationToken ct = default);

    Task<Assinatura?> ObterPorIdAsync(Guid id, CancellationToken ct = default);

    Task<Assinatura?> ObterPorEmailAsync(string email, CancellationToken ct = default);

    Task<Assinatura?> ObterPorReferenciaGatewayAsync(string referencia, CancellationToken ct = default);

    Task SalvarAsync(Assinatura assinatura, CancellationToken ct = default);
}

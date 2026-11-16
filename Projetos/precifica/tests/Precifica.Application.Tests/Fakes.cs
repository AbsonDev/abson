using Precifica.Application.Abstractions;
using Precifica.Domain.Pagamentos;

namespace Precifica.Application.Tests;

internal sealed class PedidoRepositorioFake : IPedidoRepository
{
    public List<Pedido> Pedidos { get; } = [];

    public Task AdicionarAsync(Pedido pedido, CancellationToken ct = default)
    {
        Pedidos.Add(pedido);
        return Task.CompletedTask;
    }

    public Task<Pedido?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Pedidos.FirstOrDefault(pedido => pedido.Id == id));

    public Task<bool> ExistePagoParaCalculoAsync(Guid calculoId, CancellationToken ct = default) =>
        Task.FromResult(Pedidos.Any(pedido =>
            pedido.CalculoId == calculoId && pedido.Status == StatusDoPedido.Pago));

    public Task SalvarAsync(Pedido pedido, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class AssinaturaRepositorioFake : IAssinaturaRepository
{
    public List<Assinatura> Assinaturas { get; } = [];

    public Task AdicionarAsync(Assinatura assinatura, CancellationToken ct = default)
    {
        Assinaturas.Add(assinatura);
        return Task.CompletedTask;
    }

    public Task<Assinatura?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Assinaturas.FirstOrDefault(assinatura => assinatura.Id == id));

    public Task<Assinatura?> ObterPorEmailAsync(string email, CancellationToken ct = default) =>
        Task.FromResult(Assinaturas
            .Where(assinatura => assinatura.Email == email.Trim().ToLowerInvariant())
            .OrderByDescending(assinatura => assinatura.CriadaEm)
            .FirstOrDefault());

    public Task<Assinatura?> ObterPorReferenciaGatewayAsync(string referencia, CancellationToken ct = default) =>
        Task.FromResult(Assinaturas.FirstOrDefault(assinatura => assinatura.ReferenciaGateway == referencia));

    public Task SalvarAsync(Assinatura assinatura, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class RelogioFixo(DateTimeOffset agora) : IRelogio
{
    public DateTimeOffset Agora { get; set; } = agora;
}

internal sealed class DispatcherFake : IDomainEventDispatcher
{
    public List<Domain.Common.IDomainEvent> Publicados { get; } = [];

    public Task PublicarAsync(IReadOnlyCollection<Domain.Common.IDomainEvent> eventos, CancellationToken ct = default)
    {
        Publicados.AddRange(eventos);
        return Task.CompletedTask;
    }
}

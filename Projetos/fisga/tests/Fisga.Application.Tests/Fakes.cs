using Fisga.Application.Abstractions;
using Fisga.Domain.Pescarias;

namespace Fisga.Application.Tests;

internal sealed class PescariaRepositorioFake : IPescariaRepository
{
    public List<Pescaria> Pescarias { get; } = [];

    public Task AdicionarAsync(Pescaria pescaria, CancellationToken ct = default)
    {
        Pescarias.Add(pescaria);
        return Task.CompletedTask;
    }

    public Task<Pescaria?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Pescarias.FirstOrDefault(pescaria => pescaria.Id == id));

    public Task<Pescaria?> ObterPorShareTokenAsync(string shareToken, CancellationToken ct = default) =>
        Task.FromResult(Pescarias.FirstOrDefault(pescaria => pescaria.ShareToken == shareToken));

    public Task<IReadOnlyList<Pescaria>> ListarPorDiarioAsync(string diarioTokenHash, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Pescaria>>(Pescarias
            .Where(pescaria => pescaria.DiarioTokenHash == diarioTokenHash)
            .ToList());

    public Task SalvarAsync(Pescaria pescaria, CancellationToken ct = default) => Task.CompletedTask;
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

using CardapioNoLucro.Application.Abstractions;
using CardapioNoLucro.Domain.Fichas;

namespace CardapioNoLucro.Application.Tests;

internal sealed class FichaRepositorioFake : IFichaRepository
{
    public List<FichaTecnica> Fichas { get; } = [];

    public Task AdicionarAsync(FichaTecnica ficha, CancellationToken ct = default)
    {
        Fichas.Add(ficha);
        return Task.CompletedTask;
    }

    public Task<FichaTecnica?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Fichas.FirstOrDefault(ficha => ficha.Id == id));

    public Task<FichaTecnica?> ObterPorShareTokenAsync(string shareToken, CancellationToken ct = default) =>
        Task.FromResult(Fichas.FirstOrDefault(ficha => ficha.ShareToken == shareToken));

    public Task SalvarAsync(FichaTecnica ficha, CancellationToken ct = default) => Task.CompletedTask;
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

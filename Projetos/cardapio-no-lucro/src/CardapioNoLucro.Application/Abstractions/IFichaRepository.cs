using CardapioNoLucro.Domain.Fichas;

namespace CardapioNoLucro.Application.Abstractions;

public interface IFichaRepository
{
    Task AdicionarAsync(FichaTecnica ficha, CancellationToken ct = default);

    Task<FichaTecnica?> ObterPorIdAsync(Guid id, CancellationToken ct = default);

    Task<FichaTecnica?> ObterPorShareTokenAsync(string shareToken, CancellationToken ct = default);

    Task SalvarAsync(FichaTecnica ficha, CancellationToken ct = default);
}

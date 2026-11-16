using Precifica.Domain.Precificacao;

namespace Precifica.Application.Abstractions;

public interface ICalculoRepository
{
    Task AdicionarAsync(Calculo calculo, CancellationToken ct = default);

    Task<Calculo?> ObterPorIdAsync(Guid id, CancellationToken ct = default);

    Task<Calculo?> ObterPorShareTokenAsync(string shareToken, CancellationToken ct = default);

    Task SalvarAsync(Calculo calculo, CancellationToken ct = default);
}

using Fisga.Domain.Pontos;

namespace Fisga.Application.Pontos;

public interface IPontoPrivadoRepository
{
    Task<IReadOnlyList<PontoPrivado>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    Task<PontoPrivado?> ObterPorIdAsync(Guid id, CancellationToken ct = default);

    Task<int> ContarPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    Task AdicionarAsync(PontoPrivado ponto, CancellationToken ct = default);

    Task SalvarAsync(PontoPrivado ponto, CancellationToken ct = default);

    Task RemoverAsync(PontoPrivado ponto, CancellationToken ct = default);
}

public sealed record EntradaDePonto(
    string? Nome,
    string? Descricao,
    double Latitude,
    double Longitude,
    TipoDePonto Tipo);

public sealed record PontoDto(
    Guid Id,
    string Nome,
    string? Descricao,
    double Latitude,
    double Longitude,
    string Tipo,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm);

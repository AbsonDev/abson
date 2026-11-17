using Fisga.Domain.Locais;
using Fisga.Domain.Pescarias;

namespace Fisga.Application.Locais;

public interface ILocalDePescaRepository
{
    Task<IReadOnlyList<LocalDePesca>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    Task<LocalDePesca?> ObterPorIdAsync(Guid id, CancellationToken ct = default);

    Task<int> ContarPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    Task AdicionarAsync(LocalDePesca local, CancellationToken ct = default);

    Task SalvarAsync(LocalDePesca local, CancellationToken ct = default);

    Task RemoverAsync(LocalDePesca local, CancellationToken ct = default);

    Task DesmarcarPadraoAsync(Guid usuarioId, CancellationToken ct = default);
}

public sealed record EntradaDeLocal(
    string? Nome,
    string? Cidade,
    string? Estado,
    double Latitude,
    double Longitude,
    AmbienteDePesca Ambiente,
    bool Padrao = false);

public sealed record LocalDto(
    Guid Id,
    string Nome,
    string? Cidade,
    string? Estado,
    double Latitude,
    double Longitude,
    string Ambiente,
    bool Padrao,
    DateTimeOffset CriadoEm);

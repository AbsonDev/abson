using Fisga.Domain.Trilhas;

namespace Fisga.Application.Trilhas;

public interface ITrilhaPrivadaRepository
{
    Task<IReadOnlyList<TrilhaPrivada>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    Task<TrilhaPrivada?> ObterPorIdAsync(Guid id, CancellationToken ct = default);

    Task<int> ContarPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    Task AdicionarAsync(TrilhaPrivada trilha, CancellationToken ct = default);

    Task SalvarAsync(TrilhaPrivada trilha, CancellationToken ct = default);

    Task RemoverAsync(TrilhaPrivada trilha, CancellationToken ct = default);
}

public sealed record EntradaDeTrilha(
    string? Nome,
    string? Descricao,
    TipoDeTrilha Tipo,
    IReadOnlyList<Coordenada>? Pontos);

public sealed record TrilhaDto(
    Guid Id,
    string Nome,
    string? Descricao,
    string Tipo,
    IReadOnlyList<Coordenada> Pontos,
    double DistanciaKm,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm)
{
    public static TrilhaDto De(TrilhaPrivada trilha) =>
        new(
            trilha.Id,
            trilha.Nome,
            trilha.Descricao,
            trilha.Tipo.ToString(),
            trilha.Pontos,
            trilha.DistanciaKm,
            trilha.CriadoEm,
            trilha.AtualizadoEm);
}

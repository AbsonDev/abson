using Fisga.Application.Abstractions;
using Fisga.Domain.Common;
using Fisga.Domain.Pontos;

namespace Fisga.Application.Pontos;

public sealed class ServicoDePontos(
    IPontoPrivadoRepository pontos,
    IRelogio relogio)
{
    public const int LimitePorUsuario = 200;

    public async Task<IReadOnlyList<PontoDto>> ListarAsync(
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var lista = await pontos.ListarPorUsuarioAsync(usuarioId, ct);
        return lista
            .OrderByDescending(ponto => ponto.AtualizadoEm)
            .Select(ParaDto)
            .ToList();
    }

    public async Task<PontoDto> CriarAsync(
        EntradaDePonto entrada,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var total = await pontos.ContarPorUsuarioAsync(usuarioId, ct);
        if (total >= LimitePorUsuario)
        {
            throw new DomainException(
                "limite_de_pontos",
                $"Você pode salvar até {LimitePorUsuario} pontos privados.");
        }

        var ponto = new PontoPrivado(
            Guid.NewGuid(),
            usuarioId,
            entrada.Nome ?? string.Empty,
            entrada.Descricao,
            entrada.Latitude,
            entrada.Longitude,
            entrada.Tipo,
            relogio.Agora);

        await pontos.AdicionarAsync(ponto, ct);
        return ParaDto(ponto);
    }

    public async Task<PontoDto> AtualizarAsync(
        Guid id,
        EntradaDePonto entrada,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var ponto = await ObterDoUsuarioAsync(id, usuarioId, ct);
        ponto.Atualizar(
            entrada.Nome ?? string.Empty,
            entrada.Descricao,
            entrada.Latitude,
            entrada.Longitude,
            entrada.Tipo,
            relogio.Agora);

        await pontos.SalvarAsync(ponto, ct);
        return ParaDto(ponto);
    }

    public async Task ExcluirAsync(
        Guid id,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var ponto = await ObterDoUsuarioAsync(id, usuarioId, ct);
        await pontos.RemoverAsync(ponto, ct);
    }

    private async Task<PontoPrivado> ObterDoUsuarioAsync(
        Guid id,
        Guid usuarioId,
        CancellationToken ct)
    {
        var ponto = await pontos.ObterPorIdAsync(id, ct);
        if (ponto is null)
        {
            throw new RecursoNaoEncontradoException("ponto_nao_encontrado", "Ponto não encontrado.");
        }

        if (ponto.UsuarioId != usuarioId)
        {
            throw new AcessoNegadoException("Este ponto pertence a outra conta.");
        }

        return ponto;
    }

    private static PontoDto ParaDto(PontoPrivado ponto) =>
        new(
            ponto.Id,
            ponto.Nome,
            ponto.Descricao,
            ponto.Latitude,
            ponto.Longitude,
            ponto.Tipo.ToString(),
            ponto.CriadoEm,
            ponto.AtualizadoEm);
}

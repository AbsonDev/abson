using Fisga.Application.Abstractions;
using Fisga.Domain.Common;
using Fisga.Domain.Trilhas;

namespace Fisga.Application.Trilhas;

public sealed class ServicoDeTrilhas(
    ITrilhaPrivadaRepository trilhas,
    IRelogio relogio)
{
    public const int LimitePorUsuario = 100;

    public async Task<IReadOnlyList<TrilhaDto>> ListarAsync(
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var lista = await trilhas.ListarPorUsuarioAsync(usuarioId, ct);
        return lista
            .OrderByDescending(trilha => trilha.AtualizadoEm)
            .Select(TrilhaDto.De)
            .ToList();
    }

    public async Task<TrilhaDto> CriarAsync(
        EntradaDeTrilha entrada,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var total = await trilhas.ContarPorUsuarioAsync(usuarioId, ct);
        if (total >= LimitePorUsuario)
        {
            throw new DomainException(
                "limite_de_trilhas",
                $"Você pode salvar até {LimitePorUsuario} trilhas privadas.");
        }

        var trilha = new TrilhaPrivada(
            Guid.NewGuid(),
            usuarioId,
            entrada.Nome ?? string.Empty,
            entrada.Descricao,
            entrada.Tipo,
            entrada.Pontos ?? [],
            relogio.Agora);

        await trilhas.AdicionarAsync(trilha, ct);
        return TrilhaDto.De(trilha);
    }

    public async Task<TrilhaDto> AtualizarAsync(
        Guid id,
        EntradaDeTrilha entrada,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var trilha = await ObterDoUsuarioAsync(id, usuarioId, ct);
        trilha.Atualizar(
            entrada.Nome ?? string.Empty,
            entrada.Descricao,
            entrada.Tipo,
            entrada.Pontos ?? [],
            relogio.Agora);

        await trilhas.SalvarAsync(trilha, ct);
        return TrilhaDto.De(trilha);
    }

    public async Task ExcluirAsync(
        Guid id,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var trilha = await ObterDoUsuarioAsync(id, usuarioId, ct);
        await trilhas.RemoverAsync(trilha, ct);
    }

    private async Task<TrilhaPrivada> ObterDoUsuarioAsync(
        Guid id,
        Guid usuarioId,
        CancellationToken ct)
    {
        var trilha = await trilhas.ObterPorIdAsync(id, ct);
        if (trilha is null)
        {
            throw new RecursoNaoEncontradoException("trilha_nao_encontrada", "Trilha não encontrada.");
        }

        if (trilha.UsuarioId != usuarioId)
        {
            throw new AcessoNegadoException("Esta trilha pertence a outra conta.");
        }

        return trilha;
    }
}

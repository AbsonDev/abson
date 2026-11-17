using Fisga.Application.Abstractions;
using Fisga.Domain.Common;
using Fisga.Domain.Locais;

namespace Fisga.Application.Locais;

public sealed class ServicoDeLocais(
    ILocalDePescaRepository locais,
    IRelogio relogio)
{
    public const int LimitePorUsuario = 5;

    public async Task<IReadOnlyList<LocalDto>> ListarAsync(
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var lista = await locais.ListarPorUsuarioAsync(usuarioId, ct);

        return lista
            .OrderByDescending(local => local.Padrao)
            .ThenByDescending(local => local.AtualizadoEm)
            .Select(ParaDto)
            .ToList();
    }

    public async Task<LocalDto> CriarAsync(
        EntradaDeLocal entrada,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var total = await locais.ContarPorUsuarioAsync(usuarioId, ct);
        if (total >= LimitePorUsuario)
        {
            throw new DomainException(
                "limite_de_locais",
                $"Você pode salvar até {LimitePorUsuario} locais. Remova um para adicionar outro.");
        }

        var agora = relogio.Agora;
        var local = new LocalDePesca(
            Guid.NewGuid(),
            usuarioId,
            entrada.Nome ?? string.Empty,
            entrada.Cidade,
            entrada.Estado,
            entrada.Latitude,
            entrada.Longitude,
            entrada.Ambiente,
            agora);

        if (entrada.Padrao || total == 0)
        {
            await locais.DesmarcarPadraoAsync(usuarioId, ct);
            local.DefinirPadrao(true, agora);
        }

        await locais.AdicionarAsync(local, ct);
        return ParaDto(local);
    }

    public async Task<LocalDto> AtualizarAsync(
        Guid id,
        EntradaDeLocal entrada,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var local = await ObterDoUsuarioAsync(id, usuarioId, ct);
        var agora = relogio.Agora;

        local.Atualizar(
            entrada.Nome ?? string.Empty,
            entrada.Cidade,
            entrada.Estado,
            entrada.Latitude,
            entrada.Longitude,
            entrada.Ambiente,
            agora);

        if (entrada.Padrao)
        {
            await locais.DesmarcarPadraoAsync(usuarioId, ct);
            local.DefinirPadrao(true, agora);
        }

        await locais.SalvarAsync(local, ct);
        return ParaDto(local);
    }

    public async Task<LocalDto> DefinirPadraoAsync(
        Guid id,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var local = await ObterDoUsuarioAsync(id, usuarioId, ct);
        var agora = relogio.Agora;

        await locais.DesmarcarPadraoAsync(usuarioId, ct);
        local.DefinirPadrao(true, agora);
        await locais.SalvarAsync(local, ct);
        return ParaDto(local);
    }

    public async Task ExcluirAsync(
        Guid id,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var local = await ObterDoUsuarioAsync(id, usuarioId, ct);
        var eraPadrao = local.Padrao;

        await locais.RemoverAsync(local, ct);

        if (!eraPadrao)
        {
            return;
        }

        var restantes = await locais.ListarPorUsuarioAsync(usuarioId, ct);
        var proximo = restantes
            .OrderByDescending(item => item.AtualizadoEm)
            .FirstOrDefault();

        if (proximo is null)
        {
            return;
        }

        proximo.DefinirPadrao(true, relogio.Agora);
        await locais.SalvarAsync(proximo, ct);
    }

    private async Task<LocalDePesca> ObterDoUsuarioAsync(
        Guid id,
        Guid usuarioId,
        CancellationToken ct)
    {
        var local = await locais.ObterPorIdAsync(id, ct);
        if (local is null)
        {
            throw new RecursoNaoEncontradoException("local_nao_encontrado", "Local não encontrado.");
        }

        if (local.UsuarioId != usuarioId)
        {
            throw new AcessoNegadoException("Este local pertence a outra conta.");
        }

        return local;
    }

    public static LocalDto ParaDto(LocalDePesca local) =>
        new(
            local.Id,
            local.Nome,
            local.Cidade,
            local.Estado,
            local.Latitude,
            local.Longitude,
            local.Ambiente.ToString(),
            local.Padrao,
            local.CriadoEm);
}

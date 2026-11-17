using Fisga.Application.Abstractions;
using Fisga.Application.Seguranca;
using Fisga.Domain.Pescarias;

namespace Fisga.Application.Pescarias;

public sealed class ServicoDePescarias(
    IPescariaRepository repositorio,
    IRelogio relogio,
    IDomainEventDispatcher eventos)
{
    public async Task<PescariaSalvaDto> CriarAsync(
        EntradaDePescaria entrada,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new NaoAutenticadoException();
        }

        var shareToken = TokensSeguros.Novo();

        var pescaria = Pescaria.Criar(
            PescariaMapper.ParaDados(entrada),
            Guid.NewGuid(),
            usuarioId,
            shareToken,
            relogio.Agora);

        await repositorio.AdicionarAsync(pescaria, ct);
        await PublicarEventosAsync(pescaria, ct);

        return new PescariaSalvaDto(pescaria.Id, pescaria.ShareToken);
    }

    public async Task<PescariaDetalhadaDto> AtualizarAsync(
        Guid id,
        Guid usuarioId,
        EntradaDePescaria entrada,
        CancellationToken ct = default)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new NaoAutenticadoException();
        }

        var pescaria = await repositorio.ObterPorIdAsync(id, ct)
                       ?? throw new RecursoNaoEncontradoException("pescaria_nao_encontrada", "Pescaria não encontrada.");

        if (!pescaria.PertenceA(usuarioId))
        {
            throw new RecursoNaoEncontradoException("pescaria_nao_encontrada", "Pescaria não encontrada.");
        }

        pescaria.Atualizar(PescariaMapper.ParaDados(entrada), relogio.Agora);
        await repositorio.SalvarAsync(pescaria, ct);
        await PublicarEventosAsync(pescaria, ct);

        return PescariaMapper.ParaDetalhada(pescaria, podeEditar: true);
    }

    public async Task<PescariaDetalhadaDto> ObterDoUsuarioAsync(
        Guid id,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new NaoAutenticadoException();
        }

        var pescaria = await repositorio.ObterPorIdAsync(id, ct)
                       ?? throw new RecursoNaoEncontradoException("pescaria_nao_encontrada", "Pescaria não encontrada.");

        if (!pescaria.PertenceA(usuarioId))
        {
            // Não revelamos se a pescaria existe para outro usuário.
            throw new RecursoNaoEncontradoException("pescaria_nao_encontrada", "Pescaria não encontrada.");
        }

        return PescariaMapper.ParaDetalhada(pescaria, podeEditar: true);
    }

    public async Task<PescariaDetalhadaDto?> ObterPublicoAsync(
        string shareToken,
        Guid? usuarioId = null,
        CancellationToken ct = default)
    {
        var pescaria = await repositorio.ObterPorShareTokenAsync(shareToken, ct);
        if (pescaria is null)
        {
            return null;
        }

        var podeEditar = usuarioId is { } id && pescaria.PertenceA(id);
        return PescariaMapper.ParaDetalhada(pescaria, podeEditar);
    }

    private async Task PublicarEventosAsync(Pescaria pescaria, CancellationToken ct)
    {
        if (pescaria.Eventos.Count == 0)
        {
            return;
        }

        await eventos.PublicarAsync(pescaria.Eventos, ct);
        pescaria.LimparEventos();
    }
}

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
        string diarioToken,
        CancellationToken ct = default)
    {
        var diarioHash = HashDoDiario(diarioToken);
        var shareToken = TokensSeguros.Novo();
        var editToken = TokensSeguros.Novo(24);

        var pescaria = Pescaria.Criar(
            PescariaMapper.ParaDados(entrada),
            Guid.NewGuid(),
            diarioHash,
            shareToken,
            TokensSeguros.Hash(editToken),
            relogio.Agora);

        await repositorio.AdicionarAsync(pescaria, ct);
        await PublicarEventosAsync(pescaria, ct);

        return new PescariaSalvaDto(pescaria.Id, pescaria.ShareToken, editToken);
    }

    public async Task<PescariaDetalhadaDto> AtualizarAsync(
        Guid id,
        string editToken,
        EntradaDePescaria entrada,
        CancellationToken ct = default)
    {
        var pescaria = await repositorio.ObterPorIdAsync(id, ct)
                       ?? throw new RecursoNaoEncontradoException("pescaria_nao_encontrada", "Pescaria não encontrada.");

        if (!TokensSeguros.Correspondem(editToken, pescaria.EditTokenHash))
        {
            throw new AcessoNegadoException("Você não tem permissão para editar esta pescaria.");
        }

        pescaria.Atualizar(PescariaMapper.ParaDados(entrada), relogio.Agora);
        await repositorio.SalvarAsync(pescaria, ct);
        await PublicarEventosAsync(pescaria, ct);

        return PescariaMapper.ParaDetalhada(pescaria);
    }

    public async Task<PescariaDetalhadaDto?> ObterPublicoAsync(string shareToken, CancellationToken ct = default)
    {
        var pescaria = await repositorio.ObterPorShareTokenAsync(shareToken, ct);
        return pescaria is null ? null : PescariaMapper.ParaDetalhada(pescaria);
    }

    public static string HashDoDiario(string diarioToken)
    {
        if (string.IsNullOrWhiteSpace(diarioToken) || diarioToken.Trim().Length < 16)
        {
            throw new Domain.Common.DomainException(
                "diario_token_invalido",
                "O diário deste navegador é inválido. Recarregue a página para criar um novo.");
        }

        return TokensSeguros.Hash(diarioToken.Trim());
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

using CardapioNoLucro.Application.Abstractions;
using CardapioNoLucro.Application.Catalogo;
using CardapioNoLucro.Application.Seguranca;
using CardapioNoLucro.Domain.Fichas;

namespace CardapioNoLucro.Application.Fichas;

public sealed class ServicoDeFichas(
    IFichaRepository repositorio,
    ICatalogoDeCanais catalogo,
    IRelogio relogio,
    IDomainEventDispatcher eventos)
{
    public ResultadoDto Simular(EntradaDeFicha entrada)
    {
        var ficha = FichaTecnica.Criar(
            FichaMapper.ParaDados(entrada),
            Guid.NewGuid(),
            TokensSeguros.Novo(),
            TokensSeguros.Hash(TokensSeguros.Novo(24)),
            relogio.Agora);

        return FichaMapper.ParaDto(ficha.Calcular(), catalogo);
    }

    public async Task<FichaSalvaDto> CriarAsync(EntradaDeFicha entrada, CancellationToken ct = default)
    {
        var shareToken = TokensSeguros.Novo();
        var editToken = TokensSeguros.Novo(24);

        var ficha = FichaTecnica.Criar(
            FichaMapper.ParaDados(entrada),
            Guid.NewGuid(),
            shareToken,
            TokensSeguros.Hash(editToken),
            relogio.Agora);

        await repositorio.AdicionarAsync(ficha, ct);
        await PublicarEventosAsync(ficha, ct);

        return new FichaSalvaDto(ficha.Id, ficha.ShareToken, editToken);
    }

    public async Task<FichaDetalhadaDto> AtualizarAsync(
        Guid id,
        string editToken,
        EntradaDeFicha entrada,
        CancellationToken ct = default)
    {
        var ficha = await repositorio.ObterPorIdAsync(id, ct)
                    ?? throw new RecursoNaoEncontradoException("ficha_nao_encontrada", "Ficha não encontrada.");

        if (!TokensSeguros.Correspondem(editToken, ficha.EditTokenHash))
        {
            throw new AcessoNegadoException("Você não tem permissão para editar esta ficha.");
        }

        ficha.Atualizar(FichaMapper.ParaDados(entrada), relogio.Agora);
        await repositorio.SalvarAsync(ficha, ct);
        await PublicarEventosAsync(ficha, ct);

        return ParaDetalhada(ficha);
    }

    public async Task<FichaDetalhadaDto?> ObterPublicoAsync(string shareToken, CancellationToken ct = default)
    {
        var ficha = await repositorio.ObterPorShareTokenAsync(shareToken, ct);
        return ficha is null ? null : ParaDetalhada(ficha);
    }

    private FichaDetalhadaDto ParaDetalhada(FichaTecnica ficha) =>
        new(
            ficha.Id,
            ficha.ShareToken,
            ficha.NomeDoPrato,
            ficha.Estabelecimento,
            ficha.Categoria,
            ficha.CriadoEm,
            ficha.AtualizadoEm,
            FichaMapper.ParaEntrada(ficha),
            FichaMapper.ParaDto(ficha.Calcular(), catalogo));

    private async Task PublicarEventosAsync(FichaTecnica ficha, CancellationToken ct)
    {
        if (ficha.Eventos.Count == 0)
        {
            return;
        }

        await eventos.PublicarAsync(ficha.Eventos, ct);
        ficha.LimparEventos();
    }
}

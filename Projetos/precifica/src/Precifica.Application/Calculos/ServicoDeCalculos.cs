using Precifica.Application.Abstractions;
using Precifica.Application.Calculos;
using Precifica.Application.Catalogo;
using Precifica.Application.Seguranca;
using Precifica.Domain.Precificacao;

namespace Precifica.Application.Calculos;

public sealed class ServicoDeCalculos(
    ICalculoRepository repositorio,
    ICatalogoDeNichos catalogo,
    IRelogio relogio,
    IDomainEventDispatcher eventos)
{
    public ResultadoDto Simular(EntradaDeCalculo entrada)
    {
        var calculo = Calculo.Criar(
            CalculoMapper.ParaDados(entrada),
            Guid.NewGuid(),
            TokensSeguros.Novo(),
            TokensSeguros.Hash(TokensSeguros.Novo(24)),
            relogio.Agora);

        return CalculoMapper.ParaDto(calculo.Calcular());
    }

    public async Task<CalculoSalvoDto> CriarAsync(EntradaDeCalculo entrada, CancellationToken ct = default)
    {
        var shareToken = TokensSeguros.Novo();
        var editToken = TokensSeguros.Novo(24);

        var calculo = Calculo.Criar(
            CalculoMapper.ParaDados(entrada),
            Guid.NewGuid(),
            shareToken,
            TokensSeguros.Hash(editToken),
            relogio.Agora);

        await repositorio.AdicionarAsync(calculo, ct);
        await PublicarEventosAsync(calculo, ct);

        return new CalculoSalvoDto(calculo.Id, calculo.ShareToken, editToken);
    }

    public async Task<CalculoDetalhadoDto> AtualizarAsync(
        Guid id,
        string editToken,
        EntradaDeCalculo entrada,
        CancellationToken ct = default)
    {
        var calculo = await repositorio.ObterPorIdAsync(id, ct)
                      ?? throw new RecursoNaoEncontradoException("calculo_nao_encontrado", "Cálculo não encontrado.");

        if (!TokensSeguros.Correspondem(editToken, calculo.EditTokenHash))
        {
            throw new AcessoNegadoException("Você não tem permissão para editar este cálculo.");
        }

        calculo.Atualizar(CalculoMapper.ParaDados(entrada), relogio.Agora);
        await repositorio.SalvarAsync(calculo, ct);
        await PublicarEventosAsync(calculo, ct);

        return ParaDetalhado(calculo);
    }

    public async Task<CalculoDetalhadoDto?> ObterPublicoAsync(string shareToken, CancellationToken ct = default)
    {
        var calculo = await repositorio.ObterPorShareTokenAsync(shareToken, ct);
        return calculo is null ? null : ParaDetalhado(calculo);
    }

    private CalculoDetalhadoDto ParaDetalhado(Calculo calculo) =>
        new(
            calculo.Id,
            calculo.ShareToken,
            calculo.Titulo,
            calculo.Nicho,
            catalogo.NomeDe(calculo.Nicho),
            calculo.QuantidadeProduzida,
            calculo.CriadoEm,
            calculo.AtualizadoEm,
            CalculoMapper.ParaEntrada(calculo),
            CalculoMapper.ParaDto(calculo.Calcular()));

    private async Task PublicarEventosAsync(Calculo calculo, CancellationToken ct)
    {
        if (calculo.Eventos.Count == 0)
        {
            return;
        }

        await eventos.PublicarAsync(calculo.Eventos, ct);
        calculo.LimparEventos();
    }
}

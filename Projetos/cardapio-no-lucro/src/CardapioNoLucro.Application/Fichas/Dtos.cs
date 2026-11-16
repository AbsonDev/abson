using CardapioNoLucro.Application.Catalogo;
using CardapioNoLucro.Domain.Common;
using CardapioNoLucro.Domain.Fichas;

namespace CardapioNoLucro.Application.Fichas;

public sealed record FichaSalvaDto(Guid Id, string ShareToken, string EditToken);

public sealed record FichaDetalhadaDto(
    Guid Id,
    string ShareToken,
    string NomeDoPrato,
    string? Estabelecimento,
    string? Categoria,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm,
    EntradaDeFicha Entrada,
    ResultadoDto Resultado);

public sealed record ResultadoDto(
    decimal CustoIngredientesLote,
    decimal CustoIngredientesPorcao,
    decimal CustoEmbalagemPorcao,
    decimal CustoMaoDeObraPorcao,
    decimal CustoFixoPorcao,
    decimal CustoTotalPorcao,
    decimal CmvAlvoPercentual,
    int RendimentoEmPorcoes,
    IReadOnlyList<ResultadoCanalDto> Canais);

public sealed record ResultadoCanalDto(
    string Tipo,
    string Nome,
    decimal ComissaoPercentual,
    decimal TaxaFixaPorPedido,
    decimal PrecoPorCmv,
    decimal PrecoMinimo,
    decimal PrecoSugerido,
    decimal Impostos,
    decimal ComissaoValor,
    decimal TaxaFixaAplicada,
    decimal CmvRealizadoPercentual,
    decimal MargemContribuicao,
    decimal MargemContribuicaoPercentual,
    decimal LucroOperacional,
    bool AjustadoPeloCustoTotal);

public static class FichaMapper
{
    public static DadosDaFicha ParaDados(EntradaDeFicha entrada) =>
        new()
        {
            NomeDoPrato = entrada.NomeDoPrato ?? string.Empty,
            Estabelecimento = entrada.Estabelecimento,
            Categoria = entrada.Categoria,
            RendimentoEmPorcoes = entrada.RendimentoEmPorcoes,
            PorcoesVendidasPorMes = entrada.PorcoesVendidasPorMes,
            Ingredientes = (entrada.Ingredientes ?? [])
                .Select(item => new Ingrediente(
                    item.Descricao ?? string.Empty,
                    item.Quantidade,
                    item.Unidade ?? string.Empty,
                    Dinheiro.DeReais(item.PrecoUnitario),
                    Percentual.De(item.RendimentoPercentual)))
                .ToList(),
            Embalagens = (entrada.Embalagens ?? [])
                .Select(item => new Embalagem(
                    item.Descricao ?? string.Empty,
                    Dinheiro.DeReais(item.CustoPorPorcao)))
                .ToList(),
            MinutosDeProducao = entrada.MinutosDeProducao,
            ValorHoraMaoDeObra = Dinheiro.DeReais(entrada.ValorHoraMaoDeObra),
            CustosFixosMensais = Dinheiro.DeReais(entrada.CustosFixosMensais),
            Imposto = Percentual.De(entrada.ImpostoPercentual),
            CmvAlvo = Percentual.De(entrada.CmvAlvoPercentual),
            Canais = (entrada.Canais ?? [])
                .Select(canal => new CanalDeVenda(
                    canal.Tipo,
                    Percentual.De(canal.ComissaoPercentual),
                    Dinheiro.DeReais(canal.TaxaFixaPorPedido),
                    canal.Ativo))
                .ToList(),
            Arredondamento = entrada.Arredondamento
        };

    public static EntradaDeFicha ParaEntrada(FichaTecnica ficha) =>
        new()
        {
            NomeDoPrato = ficha.NomeDoPrato,
            Estabelecimento = ficha.Estabelecimento,
            Categoria = ficha.Categoria,
            RendimentoEmPorcoes = ficha.RendimentoEmPorcoes,
            PorcoesVendidasPorMes = ficha.PorcoesVendidasPorMes,
            Ingredientes = ficha.Ingredientes
                .OrderBy(item => item.Descricao, StringComparer.CurrentCultureIgnoreCase)
                .Select(item => new EntradaIngrediente(
                    item.Descricao,
                    item.Quantidade,
                    item.Unidade,
                    item.PrecoUnitario.Reais,
                    item.Rendimento.Valor))
                .ToList(),
            Embalagens = ficha.Embalagens
                .OrderBy(item => item.Descricao, StringComparer.CurrentCultureIgnoreCase)
                .Select(item => new EntradaEmbalagem(item.Descricao, item.CustoPorPorcao.Reais))
                .ToList(),
            MinutosDeProducao = ficha.MinutosDeProducao,
            ValorHoraMaoDeObra = ficha.ValorHoraMaoDeObra.Reais,
            CustosFixosMensais = ficha.CustosFixosMensais.Reais,
            ImpostoPercentual = ficha.Imposto.Valor,
            CmvAlvoPercentual = ficha.CmvAlvo.Valor,
            Canais = ficha.Canais
                .Select(canal => new EntradaCanal(
                    canal.Tipo,
                    canal.Comissao.Valor,
                    canal.TaxaFixaPorPedido.Reais,
                    canal.Ativo))
                .ToList(),
            Arredondamento = ficha.Arredondamento
        };

    public static ResultadoDto ParaDto(ResultadoDaFicha resultado, ICatalogoDeCanais catalogo) =>
        new(
            resultado.CustoIngredientesLote.Reais,
            resultado.CustoIngredientesPorcao.Reais,
            resultado.CustoEmbalagemPorcao.Reais,
            resultado.CustoMaoDeObraPorcao.Reais,
            resultado.CustoFixoPorcao.Reais,
            resultado.CustoTotalPorcao.Reais,
            resultado.CmvAlvo.Valor,
            resultado.RendimentoEmPorcoes,
            resultado.Canais
                .Select(canal => new ResultadoCanalDto(
                    canal.Tipo.ToString(),
                    catalogo.NomeDe(canal.Tipo),
                    canal.Comissao.Valor,
                    canal.TaxaFixaPorPedido.Reais,
                    canal.PrecoPorCmv.Reais,
                    canal.PrecoMinimo.Reais,
                    canal.PrecoSugerido.Reais,
                    canal.Impostos.Reais,
                    canal.ComissaoValor.Reais,
                    canal.TaxaFixaAplicada.Reais,
                    canal.CmvRealizado.Valor,
                    canal.MargemContribuicao.Reais,
                    canal.MargemContribuicaoPercentual.Valor,
                    canal.LucroOperacional.Reais,
                    canal.AjustadoPeloCustoTotal))
                .ToList());
}

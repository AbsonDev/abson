using CardapioNoLucro.Domain.Common;

namespace CardapioNoLucro.Domain.Fichas;

public sealed record ResultadoDaFicha(
    Dinheiro CustoIngredientesLote,
    Dinheiro CustoIngredientesPorcao,
    Dinheiro CustoEmbalagemPorcao,
    Dinheiro CustoMaoDeObraPorcao,
    Dinheiro CustoFixoPorcao,
    Dinheiro CustoTotalPorcao,
    Percentual CmvAlvo,
    int RendimentoEmPorcoes,
    IReadOnlyList<ResultadoPorCanal> Canais);

public sealed record ResultadoPorCanal(
    TipoDeCanal Tipo,
    Percentual Comissao,
    Dinheiro TaxaFixaPorPedido,
    Dinheiro PrecoPorCmv,
    Dinheiro PrecoMinimo,
    Dinheiro PrecoSugerido,
    Dinheiro Impostos,
    Dinheiro ComissaoValor,
    Dinheiro TaxaFixaAplicada,
    Percentual CmvRealizado,
    Dinheiro MargemContribuicao,
    Percentual MargemContribuicaoPercentual,
    Dinheiro LucroOperacional,
    bool AjustadoPeloCustoTotal);

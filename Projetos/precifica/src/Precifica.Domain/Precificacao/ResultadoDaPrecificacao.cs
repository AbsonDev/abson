using Precifica.Domain.Common;

namespace Precifica.Domain.Precificacao;

/// <summary>
/// Resultado imutável de uma precificação. Não é persistido: é recalculado a
/// partir do agregado e pode evoluir com o tempo.
/// </summary>
public sealed record ResultadoDaPrecificacao(
    Dinheiro CustoDeMateriais,
    Dinheiro PerdaDeMaterial,
    Dinheiro CustoDeMaoDeObra,
    Dinheiro CustoFixoRateado,
    Dinheiro CustosExtras,
    Dinheiro CustoTotal,
    Dinheiro Impostos,
    Dinheiro TaxaDoCartao,
    Dinheiro TaxaFixa,
    Dinheiro PrecoSugerido,
    Dinheiro PrecoMinimo,
    Dinheiro LucroLiquido,
    Percentual MargemEfetiva,
    Dinheiro PrecoSugeridoPorUnidade,
    Dinheiro PrecoMinimoPorUnidade,
    Dinheiro CustoTotalPorUnidade,
    int QuantidadeProduzida);

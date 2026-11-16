using Precifica.Domain.Common;

namespace Precifica.Domain.Precificacao;

/// <summary>
/// Todos os dados de entrada de um cálculo. É um value object: não tem identidade,
/// só importa o conteúdo.
/// </summary>
public sealed record DadosDoCalculo
{
    public required Nicho Nicho { get; init; }

    public required string Titulo { get; init; }

    public int QuantidadeProduzida { get; init; } = 1;

    public IReadOnlyList<ItemDeMaterial> Materiais { get; init; } = [];

    public IReadOnlyList<CustoExtra> CustosExtras { get; init; } = [];

    /// <summary>Perda/desperdício aplicado sobre o custo dos materiais.</summary>
    public Percentual PerdaDeMaterial { get; init; } = Percentual.Zero;

    public int MinutosDeTrabalho { get; init; }

    public Dinheiro ValorHora { get; init; } = Dinheiro.Zero;

    public Dinheiro CustosFixosMensais { get; init; } = Dinheiro.Zero;

    public int HorasProdutivasPorMes { get; init; } = 160;

    public Percentual Imposto { get; init; } = Percentual.Zero;

    public Percentual TaxaCartao { get; init; } = Percentual.Zero;

    public Dinheiro TaxaFixaPorVenda { get; init; } = Dinheiro.Zero;

    /// <summary>Margem líquida desejada, calculada sobre o preço final.</summary>
    public Percentual MargemDesejada { get; init; } = Percentual.Zero;

    public Arredondamento Arredondamento { get; init; } = Arredondamento.Nenhum;
}

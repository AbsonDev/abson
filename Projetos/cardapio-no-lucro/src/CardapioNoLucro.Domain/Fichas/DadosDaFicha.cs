using CardapioNoLucro.Domain.Common;

namespace CardapioNoLucro.Domain.Fichas;

/// <summary>
/// Entrada completa de uma ficha técnica. A receita é informada em lote e o
/// rendimento em porções transforma os custos em custo por porção.
/// </summary>
public sealed record DadosDaFicha
{
    public required string NomeDoPrato { get; init; }

    public string? Estabelecimento { get; init; }

    public string? Categoria { get; init; }

    public int RendimentoEmPorcoes { get; init; } = 1;

    /// <summary>Volume mensal total do cardápio, usado para ratear o custo fixo.</summary>
    public int PorcoesVendidasPorMes { get; init; } = 1;

    public IReadOnlyList<Ingrediente> Ingredientes { get; init; } = [];

    public IReadOnlyList<Embalagem> Embalagens { get; init; } = [];

    public int MinutosDeProducao { get; init; }

    public Dinheiro ValorHoraMaoDeObra { get; init; } = Dinheiro.Zero;

    public Dinheiro CustosFixosMensais { get; init; } = Dinheiro.Zero;

    public Percentual Imposto { get; init; } = Percentual.Zero;

    /// <summary>CMV desejado sobre o preço de venda (custo dos ingredientes / preço).</summary>
    public Percentual CmvAlvo { get; init; } = Percentual.De(30m);

    public IReadOnlyList<CanalDeVenda> Canais { get; init; } = [];

    public Arredondamento Arredondamento { get; init; } = Arredondamento.Real;
}

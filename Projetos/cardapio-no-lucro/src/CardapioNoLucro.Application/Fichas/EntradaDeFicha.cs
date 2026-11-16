using CardapioNoLucro.Domain.Fichas;

namespace CardapioNoLucro.Application.Fichas;

public sealed record EntradaDeFicha
{
    public string? NomeDoPrato { get; init; }

    public string? Estabelecimento { get; init; }

    public string? Categoria { get; init; }

    public int RendimentoEmPorcoes { get; init; } = 1;

    public int PorcoesVendidasPorMes { get; init; } = 1;

    public IReadOnlyList<EntradaIngrediente>? Ingredientes { get; init; }

    public IReadOnlyList<EntradaEmbalagem>? Embalagens { get; init; }

    public int MinutosDeProducao { get; init; }

    public decimal ValorHoraMaoDeObra { get; init; }

    public decimal CustosFixosMensais { get; init; }

    public decimal ImpostoPercentual { get; init; }

    public decimal CmvAlvoPercentual { get; init; } = 30m;

    public IReadOnlyList<EntradaCanal>? Canais { get; init; }

    public Arredondamento Arredondamento { get; init; } = Arredondamento.Real;
}

public sealed record EntradaIngrediente(
    string? Descricao,
    decimal Quantidade,
    string? Unidade,
    decimal PrecoUnitario,
    decimal RendimentoPercentual = 100m);

public sealed record EntradaEmbalagem(string? Descricao, decimal CustoPorPorcao);

public sealed record EntradaCanal(
    TipoDeCanal Tipo,
    decimal ComissaoPercentual,
    decimal TaxaFixaPorPedido,
    bool Ativo = true);

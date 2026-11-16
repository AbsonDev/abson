using CardapioNoLucro.Domain.Fichas;

namespace CardapioNoLucro.Application.Catalogo;

public sealed record ModeloDeCanal(
    TipoDeCanal Tipo,
    string Nome,
    string Descricao,
    decimal ComissaoPercentual,
    decimal TaxaFixaPorPedido,
    bool Ativo);

public sealed record IngredienteSugerido(
    string Descricao,
    decimal Quantidade,
    string Unidade,
    decimal PrecoUnitario,
    decimal RendimentoPercentual);

public sealed record EmbalagemSugerida(string Descricao, decimal CustoPorPorcao);

public sealed record ModeloDeFicha(
    string NomeDoPrato,
    string Estabelecimento,
    string Categoria,
    int RendimentoEmPorcoes,
    int PorcoesVendidasPorMes,
    int MinutosDeProducao,
    decimal ValorHoraMaoDeObra,
    decimal CustosFixosMensais,
    decimal ImpostoPercentual,
    decimal CmvAlvoPercentual,
    IReadOnlyList<IngredienteSugerido> Ingredientes,
    IReadOnlyList<EmbalagemSugerida> Embalagens,
    IReadOnlyList<ModeloDeCanal> Canais,
    Arredondamento Arredondamento);

public interface ICatalogoDeCanais
{
    IReadOnlyList<ModeloDeCanal> ObterTodos();

    ModeloDeCanal? Obter(TipoDeCanal tipo);

    string NomeDe(TipoDeCanal tipo);

    ModeloDeFicha ObterExemplo();
}

/// <summary>
/// Catálogo supporting do MVP: canais e uma ficha de exemplo. No futuro pode
/// virar tabela por estabelecimento sem alterar o cálculo.
/// </summary>
public sealed class CatalogoDeCanais : ICatalogoDeCanais
{
    private static readonly IReadOnlyList<ModeloDeCanal> Modelos =
    [
        new(
            TipoDeCanal.Salao,
            "Salão",
            "Venda presencial, sem comissão de marketplace.",
            0m,
            0m,
            true),
        new(
            TipoDeCanal.Balcao,
            "Balcão",
            "Retirada no balcão, sem comissão de marketplace.",
            0m,
            0m,
            true),
        new(
            TipoDeCanal.DeliveryProprio,
            "Delivery próprio",
            "Entrega feita pela sua equipe. Ajuste a comissão se usar maquininha ou plataforma.",
            0m,
            0m,
            true),
        new(
            TipoDeCanal.IFood,
            "iFood",
            "Marketplace com comissão por pedido. Confirme a taxa no seu contrato.",
            27m,
            0m,
            true),
        new(
            TipoDeCanal.NoventaENoveFood,
            "99Food",
            "Marketplace com comissão por pedido. Confirme a taxa no seu contrato.",
            20m,
            0m,
            true)
    ];

    public IReadOnlyList<ModeloDeCanal> ObterTodos() => Modelos;

    public ModeloDeCanal? Obter(TipoDeCanal tipo) =>
        Modelos.FirstOrDefault(modelo => modelo.Tipo == tipo);

    public string NomeDe(TipoDeCanal tipo) =>
        Obter(tipo)?.Nome ?? tipo.ToString();

    public ModeloDeFicha ObterExemplo() =>
        new(
            NomeDoPrato: "Frango grelhado com arroz e legumes",
            Estabelecimento: "Cozinha do Bairro",
            Categoria: "Prato principal",
            RendimentoEmPorcoes: 10,
            PorcoesVendidasPorMes: 800,
            MinutosDeProducao: 120,
            ValorHoraMaoDeObra: 32m,
            CustosFixosMensais: 6000m,
            ImpostoPercentual: 6m,
            CmvAlvoPercentual: 30m,
            Ingredientes:
            [
                new("Peito de frango", 2.5m, "kg", 22.00m, 90m),
                new("Arroz", 1.5m, "kg", 6.00m, 100m),
                new("Feijão", 0.8m, "kg", 9.00m, 100m),
                new("Legumes", 1.0m, "kg", 12.00m, 85m),
                new("Óleo e temperos", 0.3m, "kg", 15.00m, 100m)
            ],
            Embalagens:
            [
                new("Marmita + talheres", 1.80m)
            ],
            Canais: Modelos,
            Arredondamento: Arredondamento.Real);
}

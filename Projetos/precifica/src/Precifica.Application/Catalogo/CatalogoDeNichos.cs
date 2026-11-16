using Precifica.Domain.Common;

namespace Precifica.Application.Catalogo;

public sealed record MaterialSugerido(
    string Descricao,
    decimal Quantidade,
    string Unidade,
    decimal CustoUnitario);

public sealed record CustoExtraSugerido(string Descricao, decimal Valor);

public sealed record ModeloDeNicho(
    Domain.Precificacao.Nicho Nicho,
    string Nome,
    string Emoji,
    string Descricao,
    string TituloExemplo,
    int QuantidadeExemplo,
    int MinutosDeTrabalho,
    decimal ValorHoraSugerido,
    decimal CustosFixosMensaisSugeridos,
    int HorasProdutivasPorMes,
    decimal PerdaDeMaterialPercentual,
    decimal ImpostoPercentual,
    decimal TaxaCartaoPercentual,
    decimal MargemSugerida,
    IReadOnlyList<MaterialSugerido> Materiais,
    IReadOnlyList<CustoExtraSugerido> CustosExtras,
    string DicaDePreco);

public interface ICatalogoDeNichos
{
    IReadOnlyList<ModeloDeNicho> ObterTodos();

    ModeloDeNicho? Obter(Domain.Precificacao.Nicho nicho);

    string NomeDe(Domain.Precificacao.Nicho nicho);
}

/// <summary>
/// Catálogo de nichos (contexto supporting). No MVP é estático e vive em memória;
/// quando houver curadoria/edição, vira uma tabela própria sem afetar o cálculo.
/// </summary>
public sealed class CatalogoDeNichos : ICatalogoDeNichos
{
    private static readonly IReadOnlyList<ModeloDeNicho> Modelos =
    [
        new ModeloDeNicho(
            Domain.Precificacao.Nicho.Confeitaria,
            "Confeitaria",
            "🧁",
            "Bolos, doces e sobremesas sob encomenda.",
            "Bolo de chocolate com ganache",
            1,
            120,
            35m,
            800m,
            120,
            5m,
            6m,
            4.99m,
            40m,
            [
                new MaterialSugerido("Farinha de trigo", 0.5m, "kg", 6.00m),
                new MaterialSugerido("Açúcar", 0.4m, "kg", 5.00m),
                new MaterialSugerido("Chocolate meio amargo", 0.3m, "kg", 32.00m),
                new MaterialSugerido("Manteiga", 0.2m, "kg", 45.00m),
                new MaterialSugerido("Ovos", 4m, "un", 0.80m)
            ],
            [new CustoExtraSugerido("Embalagem e etiqueta", 4.50m)],
            "Cobre a perda de massa no forno e a decoração. O preço do bolo não é só o custo dos ingredientes."),
        new ModeloDeNicho(
            Domain.Precificacao.Nicho.Artesanato,
            "Artesanato",
            "🧶",
            "Crochê, costura, bijuterias e peças feitas à mão.",
            "Bolsa de crochê",
            1,
            300,
            30m,
            300m,
            100,
            0m,
            6m,
            4.99m,
            45m,
            [
                new MaterialSugerido("Fio de algodão", 3m, "un", 12.50m),
                new MaterialSugerido("Embalagem", 1m, "un", 4.00m)
            ],
            [new CustoExtraSugerido("Etiqueta personalizada", 1.50m)],
            "Valorize cada hora de trabalho manual. Peça sob encomenda costuma justificar margem acima de 40%."),
        new ModeloDeNicho(
            Domain.Precificacao.Nicho.Impressao3D,
            "Impressão 3D",
            "🖨️",
            "Peças impressas sob demanda, protótipos e decoração.",
            "Vaso decorativo 20 cm",
            1,
            180,
            40m,
            600m,
            160,
            3m,
            6m,
            4.99m,
            40m,
            [
                new MaterialSugerido("Filamento PLA", 0.25m, "kg", 110.00m),
                new MaterialSugerido("Energia elétrica", 1m, "un", 3.00m)
            ],
            [new CustoExtraSugerido("Embalagem", 4.00m)],
            "Inclua falhas de impressão na perda de material e cobre a depreciação da impressora nos custos fixos."),
        new ModeloDeNicho(
            Domain.Precificacao.Nicho.Estetica,
            "Estética",
            "✨",
            "Limpeza de pele, design de sobrancelhas e procedimentos.",
            "Limpeza de pele profunda",
            1,
            60,
            60m,
            1500m,
            100,
            0m,
            6m,
            4.99m,
            45m,
            [
                new MaterialSugerido("Produtos de limpeza", 1m, "un", 18.00m),
                new MaterialSugerido("Descartáveis", 1m, "un", 6.00m)
            ],
            [new CustoExtraSugerido("Toalha e lençol descartável", 3.00m)],
            "Seu tempo de cadeira é o ativo mais caro do negócio. Não coloque o aluguel inteiro em um único atendimento: rateie pelas horas produtivas.")
    ];

    public IReadOnlyList<ModeloDeNicho> ObterTodos() => Modelos;

    public ModeloDeNicho? Obter(Domain.Precificacao.Nicho nicho) =>
        Modelos.FirstOrDefault(modelo => modelo.Nicho == nicho);

    public string NomeDe(Domain.Precificacao.Nicho nicho) =>
        Obter(nicho)?.Nome ?? nicho.ToString();
}

public static class CatalogoDePlanos
{
    public static readonly Dinheiro PdfAvulso = Dinheiro.DeReais(29m);

    public static readonly Dinheiro ProMensal = Dinheiro.DeReais(9m);

    public const string NomeDoPlanoPro = "Precifica Pro";
}

using Precifica.Domain.Common;
using Precifica.Domain.Precificacao.Eventos;

namespace Precifica.Domain.Precificacao;

/// <summary>
/// Raiz do contexto de Precificação. Garante as invariantes do cálculo e concentra
/// a fórmula de preço — a razão de existir do produto.
/// </summary>
public sealed class Calculo : AggregateRoot
{
    private readonly List<ItemDeMaterial> _materiais = [];
    private readonly List<CustoExtra> _custosExtras = [];

    private Calculo()
    {
    }

    private Calculo(Guid id, string shareToken, string editTokenHash, DateTimeOffset criadoEm)
    {
        Id = id;
        ShareToken = shareToken;
        EditTokenHash = editTokenHash;
        CriadoEm = criadoEm;
        AtualizadoEm = criadoEm;
    }

    public Guid Id { get; private set; }

    /// <summary>Token público usado no link compartilhável e no PDF.</summary>
    public string ShareToken { get; private set; } = string.Empty;

    /// <summary>Hash do token privado de edição. O valor cru nunca é persistido.</summary>
    public string EditTokenHash { get; private set; } = string.Empty;

    public Nicho Nicho { get; private set; }

    public string Titulo { get; private set; } = string.Empty;

    public int QuantidadeProduzida { get; private set; }

    public Percentual PerdaDeMaterial { get; private set; }

    public int MinutosDeTrabalho { get; private set; }

    public Dinheiro ValorHora { get; private set; }

    public Dinheiro CustosFixosMensais { get; private set; }

    public int HorasProdutivasPorMes { get; private set; }

    public Percentual Imposto { get; private set; }

    public Percentual TaxaCartao { get; private set; }

    public Dinheiro TaxaFixaPorVenda { get; private set; }

    public Percentual MargemDesejada { get; private set; }

    public Arredondamento Arredondamento { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset AtualizadoEm { get; private set; }

    public IReadOnlyCollection<ItemDeMaterial> Materiais => _materiais;

    public IReadOnlyCollection<CustoExtra> CustosExtras => _custosExtras;

    public static Calculo Criar(
        DadosDoCalculo dados,
        Guid id,
        string shareToken,
        string editTokenHash,
        DateTimeOffset agora)
    {
        var calculo = new Calculo(id, shareToken, editTokenHash, agora);
        calculo.Aplicar(dados, agora);

        var resultado = calculo.Calcular();
        calculo.Registrar(new CalculoRegistrado(calculo.Id, calculo.Nicho, resultado.PrecoSugerido, agora));

        return calculo;
    }

    public ResultadoDaPrecificacao Atualizar(DadosDoCalculo dados, DateTimeOffset agora)
    {
        Aplicar(dados, agora);
        return Calcular();
    }

    /// <summary>
    /// Preço com margem desejada:
    /// (custo total + taxa fixa) / (1 - margem - impostos - taxa do cartão).
    /// Todos os percentuais incidem sobre o preço final, não sobre o custo.
    /// </summary>
    public ResultadoDaPrecificacao Calcular()
    {
        Validar();

        var materiaisBrutos = _materiais.Aggregate(Dinheiro.Zero, (total, item) => total + item.CustoTotal);
        var perda = PerdaDeMaterial.AplicarSobre(materiaisBrutos);
        var custoMateriais = materiaisBrutos + perda;

        var custoMaoDeObra = ValorHora.Multiplicar(MinutosDeTrabalho / 60m);

        var custoFixoRateado = Dinheiro.Zero;
        if (!CustosFixosMensais.EhZero && HorasProdutivasPorMes > 0)
        {
            var fracaoDoMes = MinutosDeTrabalho / (HorasProdutivasPorMes * 60m);
            custoFixoRateado = CustosFixosMensais.Multiplicar(fracaoDoMes);
        }

        var custosExtras = _custosExtras.Aggregate(Dinheiro.Zero, (total, item) => total + item.Valor);
        var custoTotal = custoMateriais + custoMaoDeObra + custoFixoRateado + custosExtras;

        if (custoTotal.EhZero)
        {
            throw new DomainException(
                "calculo_sem_custos",
                "Informe ao menos um custo ou uma quantidade de tempo para calcular o preço.");
        }

        var percentuaisSobrePreco = Imposto + TaxaCartao;
        var denominadorComMargem = 1m - (MargemDesejada + percentuaisSobrePreco).Fracao;
        var denominadorMinimo = 1m - percentuaisSobrePreco.Fracao;

        // Arredonda para cima já no centavo para nunca entregar preço abaixo da margem.
        var precoSugerido = Arredondamento.ArredondarParaCima(
            new Dinheiro(ParaCentavosParaCima(custoTotal.Centavos + TaxaFixaPorVenda.Centavos, denominadorComMargem)));

        var precoMinimo = Arredondamento.ArredondarParaCima(
            new Dinheiro(ParaCentavosParaCima(custoTotal.Centavos + TaxaFixaPorVenda.Centavos, denominadorMinimo)));

        var impostos = Imposto.AplicarSobre(precoSugerido);
        var taxaCartao = TaxaCartao.AplicarSobre(precoSugerido);
        var lucroLiquido = precoSugerido - impostos - taxaCartao - TaxaFixaPorVenda - custoTotal;

        var margemEfetiva = precoSugerido.EhZero
            ? Percentual.Zero
            : Percentual.De((decimal)lucroLiquido.Centavos / precoSugerido.Centavos * 100m);

        return new ResultadoDaPrecificacao(
            CustoDeMateriais: custoMateriais,
            PerdaDeMaterial: perda,
            CustoDeMaoDeObra: custoMaoDeObra,
            CustoFixoRateado: custoFixoRateado,
            CustosExtras: custosExtras,
            CustoTotal: custoTotal,
            Impostos: impostos,
            TaxaDoCartao: taxaCartao,
            TaxaFixa: TaxaFixaPorVenda,
            PrecoSugerido: precoSugerido,
            PrecoMinimo: precoMinimo,
            LucroLiquido: lucroLiquido,
            MargemEfetiva: margemEfetiva,
            PrecoSugeridoPorUnidade: precoSugerido.Dividir(QuantidadeProduzida),
            PrecoMinimoPorUnidade: precoMinimo.Dividir(QuantidadeProduzida),
            CustoTotalPorUnidade: custoTotal.Dividir(QuantidadeProduzida),
            QuantidadeProduzida: QuantidadeProduzida);
    }

    private void Aplicar(DadosDoCalculo dados, DateTimeOffset agora)
    {
        Nicho = dados.Nicho;
        Titulo = (dados.Titulo ?? string.Empty).Trim();
        QuantidadeProduzida = dados.QuantidadeProduzida;
        PerdaDeMaterial = dados.PerdaDeMaterial;
        MinutosDeTrabalho = dados.MinutosDeTrabalho;
        ValorHora = dados.ValorHora;
        CustosFixosMensais = dados.CustosFixosMensais;
        HorasProdutivasPorMes = dados.HorasProdutivasPorMes;
        Imposto = dados.Imposto;
        TaxaCartao = dados.TaxaCartao;
        TaxaFixaPorVenda = dados.TaxaFixaPorVenda;
        MargemDesejada = dados.MargemDesejada;
        Arredondamento = dados.Arredondamento;
        AtualizadoEm = agora;

        _materiais.Clear();
        _materiais.AddRange(dados.Materiais ?? []);

        _custosExtras.Clear();
        _custosExtras.AddRange(dados.CustosExtras ?? []);

        Validar();
    }

    private void Validar()
    {
        if (Id == Guid.Empty)
        {
            throw new DomainException("calculo_sem_identidade", "Cálculo sem identificador.");
        }

        if (string.IsNullOrWhiteSpace(ShareToken) || string.IsNullOrWhiteSpace(EditTokenHash))
        {
            throw new DomainException("calculo_sem_tokens", "Cálculo sem tokens de compartilhamento/edição.");
        }

        if (!Enum.IsDefined(Nicho))
        {
            throw new DomainException("nicho_invalido", "Escolha um nicho válido.");
        }

        if (string.IsNullOrWhiteSpace(Titulo))
        {
            throw new DomainException("titulo_obrigatorio", "Dê um nome ao que você está precificando.");
        }

        if (Titulo.Length > 140)
        {
            throw new DomainException("titulo_longo", "O nome do produto ou serviço deve ter até 140 caracteres.");
        }

        if (QuantidadeProduzida < 1 || QuantidadeProduzida > 100_000)
        {
            throw new DomainException("quantidade_invalida", "A quantidade produzida deve estar entre 1 e 100.000.");
        }

        if (MinutosDeTrabalho < 0 || MinutosDeTrabalho > 44_640)
        {
            throw new DomainException("tempo_invalido", "O tempo de trabalho deve estar entre 0 e 744 horas.");
        }

        if (ValorHora.EhNegativo || ValorHora.Centavos > 100_000_000)
        {
            throw new DomainException("valor_hora_invalido", "O valor da sua hora precisa estar entre R$ 0,00 e R$ 1.000.000,00.");
        }

        if (CustosFixosMensais.EhNegativo || CustosFixosMensais.Centavos > 10_000_000_000)
        {
            throw new DomainException("custos_fixos_invalidos", "Os custos fixos mensais precisam estar entre R$ 0,00 e R$ 100.000.000,00.");
        }

        if (HorasProdutivasPorMes < 0 || HorasProdutivasPorMes > 744)
        {
            throw new DomainException("horas_produtivas_invalidas", "As horas produtivas por mês devem estar entre 0 e 744.");
        }

        if (CustosFixosMensais > Dinheiro.Zero && HorasProdutivasPorMes == 0)
        {
            throw new DomainException(
                "horas_produtivas_invalidas",
                "Informe quantas horas por mês você realmente produz para ratear os custos fixos.");
        }

        if (TaxaFixaPorVenda.EhNegativo || TaxaFixaPorVenda.Centavos > 100_000_000)
        {
            throw new DomainException("taxa_fixa_invalida", "A taxa fixa por venda precisa estar entre R$ 0,00 e R$ 1.000.000,00.");
        }

        ValidarPercentual(PerdaDeMaterial, "perda_material_invalida", "A perda de material", 100);
        ValidarPercentual(Imposto, "imposto_invalido", "O imposto", 100);
        ValidarPercentual(TaxaCartao, "taxa_cartao_invalida", "A taxa do cartão", 100);
        ValidarPercentual(MargemDesejada, "margem_invalida", "A margem desejada", 99.99m);

        if ((MargemDesejada + Imposto + TaxaCartao).PontosBase >= 10_000)
        {
            throw new DomainException(
                "percentuais_acima_de_cem",
                "A soma da margem, impostos e taxa do cartão precisa ser menor que 100%.");
        }

        if (_materiais.Count > 200 || _custosExtras.Count > 100)
        {
            throw new DomainException("muitos_itens", "O cálculo tem itens demais. Divida em cálculos menores.");
        }

        var custoTotal = _materiais.Sum(item => item.CustoTotal.Centavos)
                         + _custosExtras.Sum(item => item.Valor.Centavos);

        if (custoTotal == 0 && MinutosDeTrabalho == 0 && CustosFixosMensais.EhZero)
        {
            throw new DomainException(
                "calculo_sem_custos",
                "Informe ao menos um custo ou uma quantidade de tempo para calcular o preço.");
        }
    }

    private static void ValidarPercentual(Percentual percentual, string codigo, string campo, decimal maximo)
    {
        if (percentual.Valor < 0 || percentual.Valor > maximo)
        {
            throw new DomainException(codigo, $"{campo} deve estar entre 0% e {maximo:0.##}%.");
        }
    }

    private static long ParaCentavosParaCima(long centavos, decimal denominador)
    {
        if (denominador <= 0)
        {
            throw new DomainException(
                "percentuais_acima_de_cem",
                "A soma dos percentuais sobre o preço precisa ser menor que 100%.");
        }

        return (long)decimal.Ceiling(centavos / denominador);
    }
}

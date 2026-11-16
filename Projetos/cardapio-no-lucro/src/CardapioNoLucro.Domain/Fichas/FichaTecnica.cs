using CardapioNoLucro.Domain.Common;
using CardapioNoLucro.Domain.Fichas.Eventos;

namespace CardapioNoLucro.Domain.Fichas;

/// <summary>
/// Raiz do contexto de Fichas Técnicas. Concentra a conta que liga CMV,
/// custos fixos e comissões de canal ao preço de venda por porção.
/// </summary>
public sealed class FichaTecnica : AggregateRoot
{
    private readonly List<Ingrediente> _ingredientes = [];
    private readonly List<Embalagem> _embalagens = [];
    private readonly List<CanalDeVenda> _canais = [];

    private FichaTecnica()
    {
    }

    private FichaTecnica(Guid id, string shareToken, string editTokenHash, DateTimeOffset criadoEm)
    {
        Id = id;
        ShareToken = shareToken;
        EditTokenHash = editTokenHash;
        CriadoEm = criadoEm;
        AtualizadoEm = criadoEm;
    }

    public Guid Id { get; private set; }

    public string ShareToken { get; private set; } = string.Empty;

    public string EditTokenHash { get; private set; } = string.Empty;

    public string NomeDoPrato { get; private set; } = string.Empty;

    public string? Estabelecimento { get; private set; }

    public string? Categoria { get; private set; }

    public int RendimentoEmPorcoes { get; private set; }

    public int PorcoesVendidasPorMes { get; private set; }

    public int MinutosDeProducao { get; private set; }

    public Dinheiro ValorHoraMaoDeObra { get; private set; }

    public Dinheiro CustosFixosMensais { get; private set; }

    public Percentual Imposto { get; private set; }

    public Percentual CmvAlvo { get; private set; }

    public Arredondamento Arredondamento { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset AtualizadoEm { get; private set; }

    public IReadOnlyCollection<Ingrediente> Ingredientes => _ingredientes;

    public IReadOnlyCollection<Embalagem> Embalagens => _embalagens;

    public IReadOnlyCollection<CanalDeVenda> Canais => _canais;

    public static FichaTecnica Criar(
        DadosDaFicha dados,
        Guid id,
        string shareToken,
        string editTokenHash,
        DateTimeOffset agora)
    {
        var ficha = new FichaTecnica(id, shareToken, editTokenHash, agora);
        ficha.Aplicar(dados, agora);
        ficha.Calcular();
        ficha.Registrar(new FichaRegistrada(ficha.Id, ficha.NomeDoPrato, agora));
        return ficha;
    }

    public ResultadoDaFicha Atualizar(DadosDaFicha dados, DateTimeOffset agora)
    {
        Aplicar(dados, agora);
        return Calcular();
    }

    /// <summary>
    /// Preço por CMV: custo dos ingredientes / CMV alvo.
    /// Preço mínimo: cobre todos os custos e as taxas do canal sem lucro.
    /// O preço sugerido é o maior dos dois.
    /// </summary>
    public ResultadoDaFicha Calcular()
    {
        Validar();

        var custoIngredientesLote = _ingredientes.Aggregate(
            Dinheiro.Zero,
            (total, ingrediente) => total + ingrediente.CustoComRendimento);

        var custoIngredientesPorcao = custoIngredientesLote.Dividir(RendimentoEmPorcoes);
        var custoEmbalagemPorcao = _embalagens.Aggregate(
            Dinheiro.Zero,
            (total, embalagem) => total + embalagem.CustoPorPorcao);

        var custoMaoDeObraLote = ValorHoraMaoDeObra.Multiplicar(MinutosDeProducao / 60m);
        var custoMaoDeObraPorcao = custoMaoDeObraLote.Dividir(RendimentoEmPorcoes);
        var custoFixoPorcao = CustosFixosMensais.Dividir(PorcoesVendidasPorMes);

        var custoTotalPorcao = custoIngredientesPorcao
                               + custoEmbalagemPorcao
                               + custoMaoDeObraPorcao
                               + custoFixoPorcao;

        var canais = _canais
            .Where(canal => canal.Ativo)
            .OrderBy(canal => canal.Tipo)
            .Select(canal => CalcularCanal(canal, custoIngredientesPorcao, custoEmbalagemPorcao, custoMaoDeObraPorcao, custoFixoPorcao, custoTotalPorcao))
            .ToList();

        return new ResultadoDaFicha(
            CustoIngredientesLote: custoIngredientesLote,
            CustoIngredientesPorcao: custoIngredientesPorcao,
            CustoEmbalagemPorcao: custoEmbalagemPorcao,
            CustoMaoDeObraPorcao: custoMaoDeObraPorcao,
            CustoFixoPorcao: custoFixoPorcao,
            CustoTotalPorcao: custoTotalPorcao,
            CmvAlvo: CmvAlvo,
            RendimentoEmPorcoes: RendimentoEmPorcoes,
            Canais: canais);
    }

    private ResultadoPorCanal CalcularCanal(
        CanalDeVenda canal,
        Dinheiro custoIngredientesPorcao,
        Dinheiro custoEmbalagemPorcao,
        Dinheiro custoMaoDeObraPorcao,
        Dinheiro custoFixoPorcao,
        Dinheiro custoTotalPorcao)
    {
        var percentuaisSobrePreco = Imposto + canal.Comissao;
        var denominadorMinimo = 1m - percentuaisSobrePreco.Fracao;

        if (denominadorMinimo <= 0)
        {
            throw new DomainException(
                "percentuais_acima_de_cem",
                "A soma do imposto com a comissão do canal precisa ser menor que 100%.");
        }

        var precoPorCmv = Arredondamento.ArredondarParaCima(
            DivisaoParaCima(custoIngredientesPorcao.Centavos, CmvAlvo.Fracao));

        var precoMinimo = Arredondamento.ArredondarParaCima(
            DivisaoParaCima(custoTotalPorcao.Centavos + canal.TaxaFixaPorPedido.Centavos, denominadorMinimo));

        var precoSugerido = precoPorCmv > precoMinimo ? precoPorCmv : precoMinimo;
        var impostos = Imposto.AplicarSobre(precoSugerido);
        var comissaoValor = canal.Comissao.AplicarSobre(precoSugerido);

        var margemContribuicao = precoSugerido
                                 - impostos
                                 - comissaoValor
                                 - canal.TaxaFixaPorPedido
                                 - custoIngredientesPorcao
                                 - custoEmbalagemPorcao;

        var lucroOperacional = margemContribuicao - custoMaoDeObraPorcao - custoFixoPorcao;

        var cmvRealizado = precoSugerido.EhZero
            ? Percentual.Zero
            : Percentual.De((decimal)custoIngredientesPorcao.Centavos / precoSugerido.Centavos * 100m);

        var margemPercentual = precoSugerido.EhZero
            ? Percentual.Zero
            : Percentual.De((decimal)margemContribuicao.Centavos / precoSugerido.Centavos * 100m);

        return new ResultadoPorCanal(
            Tipo: canal.Tipo,
            Comissao: canal.Comissao,
            TaxaFixaPorPedido: canal.TaxaFixaPorPedido,
            PrecoPorCmv: precoPorCmv,
            PrecoMinimo: precoMinimo,
            PrecoSugerido: precoSugerido,
            Impostos: impostos,
            ComissaoValor: comissaoValor,
            TaxaFixaAplicada: canal.TaxaFixaPorPedido,
            CmvRealizado: cmvRealizado,
            MargemContribuicao: margemContribuicao,
            MargemContribuicaoPercentual: margemPercentual,
            LucroOperacional: lucroOperacional,
            AjustadoPeloCustoTotal: precoMinimo > precoPorCmv);
    }

    private void Aplicar(DadosDaFicha dados, DateTimeOffset agora)
    {
        NomeDoPrato = (dados.NomeDoPrato ?? string.Empty).Trim();
        Estabelecimento = string.IsNullOrWhiteSpace(dados.Estabelecimento)
            ? null
            : dados.Estabelecimento.Trim();
        Categoria = string.IsNullOrWhiteSpace(dados.Categoria) ? null : dados.Categoria.Trim();
        RendimentoEmPorcoes = dados.RendimentoEmPorcoes;
        PorcoesVendidasPorMes = dados.PorcoesVendidasPorMes;
        MinutosDeProducao = dados.MinutosDeProducao;
        ValorHoraMaoDeObra = dados.ValorHoraMaoDeObra;
        CustosFixosMensais = dados.CustosFixosMensais;
        Imposto = dados.Imposto;
        CmvAlvo = dados.CmvAlvo;
        Arredondamento = dados.Arredondamento;
        AtualizadoEm = agora;

        _ingredientes.Clear();
        _ingredientes.AddRange(dados.Ingredientes ?? []);

        _embalagens.Clear();
        _embalagens.AddRange(dados.Embalagens ?? []);

        _canais.Clear();
        _canais.AddRange(dados.Canais ?? []);

        Validar();
    }

    private void Validar()
    {
        if (Id == Guid.Empty)
        {
            throw new DomainException("ficha_sem_identidade", "Ficha sem identificador.");
        }

        if (string.IsNullOrWhiteSpace(ShareToken) || string.IsNullOrWhiteSpace(EditTokenHash))
        {
            throw new DomainException("ficha_sem_tokens", "Ficha sem tokens de compartilhamento/edição.");
        }

        if (string.IsNullOrWhiteSpace(NomeDoPrato))
        {
            throw new DomainException("prato_sem_nome", "Dê um nome ao prato.");
        }

        if (NomeDoPrato.Length > 120)
        {
            throw new DomainException("prato_nome_longo", "O nome do prato deve ter até 120 caracteres.");
        }

        if (Estabelecimento?.Length > 120)
        {
            throw new DomainException("estabelecimento_longo", "O nome do estabelecimento deve ter até 120 caracteres.");
        }

        if (Categoria?.Length > 40)
        {
            throw new DomainException("categoria_longa", "A categoria deve ter até 40 caracteres.");
        }

        if (RendimentoEmPorcoes < 1 || RendimentoEmPorcoes > 10_000)
        {
            throw new DomainException("rendimento_invalido", "O rendimento deve estar entre 1 e 10.000 porções.");
        }

        if (PorcoesVendidasPorMes < 1 || PorcoesVendidasPorMes > 10_000_000)
        {
            throw new DomainException("porcoes_mes_invalidas", "As porções vendidas por mês devem estar entre 1 e 10.000.000.");
        }

        if (MinutosDeProducao < 0 || MinutosDeProducao > 44_640)
        {
            throw new DomainException("tempo_invalido", "O tempo de produção deve estar entre 0 e 744 horas.");
        }

        if (ValorHoraMaoDeObra.EhNegativo || ValorHoraMaoDeObra.Centavos > 100_000_000)
        {
            throw new DomainException("valor_hora_invalido", "O valor da hora deve estar entre R$ 0,00 e R$ 1.000.000,00.");
        }

        if (CustosFixosMensais.EhNegativo || CustosFixosMensais.Centavos > 10_000_000_000)
        {
            throw new DomainException("custos_fixos_invalidos", "Os custos fixos mensais devem estar entre R$ 0,00 e R$ 100.000.000,00.");
        }

        ValidarPercentual(Imposto, "imposto_invalido", "O imposto", 100m);

        if (CmvAlvo.Valor <= 0 || CmvAlvo.Valor > 90)
        {
            throw new DomainException("cmv_invalido", "O CMV alvo deve estar entre 0,01% e 90%.");
        }

        if (_ingredientes.Count == 0)
        {
            throw new DomainException("ficha_sem_ingredientes", "Inclua ao menos um ingrediente na ficha técnica.");
        }

        if (_ingredientes.Count > 200 || _embalagens.Count > 100 || _canais.Count > 10)
        {
            throw new DomainException("muitos_itens", "A ficha tem itens demais. Divida em fichas menores.");
        }

        if (_canais.Count == 0)
        {
            throw new DomainException("ficha_sem_canais", "Inclua ao menos um canal de venda.");
        }

        var tipos = new HashSet<TipoDeCanal>();
        foreach (var canal in _canais)
        {
            if (!tipos.Add(canal.Tipo))
            {
                throw new DomainException("canal_duplicado", "Cada canal de venda só pode aparecer uma vez.");
            }

            if (canal.Ativo && (Imposto + canal.Comissao).Valor >= 100)
            {
                throw new DomainException(
                    "percentuais_acima_de_cem",
                    "A soma do imposto com a comissão do canal precisa ser menor que 100%.");
            }
        }

        if (!_canais.Any(canal => canal.Ativo))
        {
            throw new DomainException("ficha_sem_canais_ativos", "Ative ao menos um canal de venda.");
        }

        var custoTotal = _ingredientes.Sum(ingrediente => ingrediente.CustoComRendimento.Centavos)
                         + _embalagens.Sum(embalagem => embalagem.CustoPorPorcao.Centavos);

        if (custoTotal == 0 && MinutosDeProducao == 0 && CustosFixosMensais.EhZero)
        {
            throw new DomainException("ficha_sem_custos", "Informe ao menos um custo ou tempo de produção.");
        }
    }

    private static void ValidarPercentual(Percentual percentual, string codigo, string campo, decimal maximo)
    {
        if (percentual.Valor < 0 || percentual.Valor > maximo)
        {
            throw new DomainException(codigo, $"{campo} deve estar entre 0% e {maximo:0.##}%.");
        }
    }

    private static Dinheiro DivisaoParaCima(long centavos, decimal denominador)
    {
        if (denominador <= 0)
        {
            throw new DomainException(
                "percentuais_acima_de_cem",
                "A soma dos percentuais sobre o preço precisa ser menor que 100%.");
        }

        return new Dinheiro((long)decimal.Ceiling(centavos / denominador));
    }
}

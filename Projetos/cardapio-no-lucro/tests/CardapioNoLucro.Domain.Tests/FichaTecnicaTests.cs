using CardapioNoLucro.Domain.Common;
using CardapioNoLucro.Domain.Fichas;

namespace CardapioNoLucro.Domain.Tests;

public sealed class FichaTecnicaTests
{
    [Fact]
    public void Calcula_custo_por_porcao_e_preco_pelo_cmv()
    {
        var ficha = Fabrica.Ficha();

        var resultado = ficha.Calcular();
        var salao = Assert.Single(resultado.Canais);

        Assert.Equal(3000, resultado.CustoIngredientesLote.Centavos);
        Assert.Equal(300, resultado.CustoIngredientesPorcao.Centavos);
        Assert.Equal(100, resultado.CustoEmbalagemPorcao.Centavos);
        Assert.Equal(300, resultado.CustoMaoDeObraPorcao.Centavos);
        Assert.Equal(100, resultado.CustoFixoPorcao.Centavos);
        Assert.Equal(800, resultado.CustoTotalPorcao.Centavos);

        // 3,00 de ingredientes / 30% de CMV = 10,00
        Assert.Equal(1000, salao.PrecoPorCmv.Centavos);
        // 8,00 de custo total / 94% (imposto de 6%) = 8,51
        Assert.Equal(852, salao.PrecoMinimo.Centavos);
        Assert.Equal(1000, salao.PrecoSugerido.Centavos);
        Assert.Equal(60, salao.Impostos.Centavos);
        Assert.Equal(540, salao.MargemContribuicao.Centavos);
        Assert.Equal(140, salao.LucroOperacional.Centavos);
        Assert.Equal(3000, salao.CmvRealizado.PontosBase);
        Assert.False(salao.AjustadoPeloCustoTotal);
    }

    [Fact]
    public void Comissao_do_ifood_eleva_o_preco_minimo()
    {
        var canais = new[]
        {
            new CanalDeVenda(TipoDeCanal.Salao, Percentual.Zero, Dinheiro.Zero),
            new CanalDeVenda(TipoDeCanal.IFood, Percentual.De(27m), Dinheiro.Zero)
        };

        var ficha = Fabrica.Ficha(Fabrica.Dados(canais: canais));
        var resultado = ficha.Calcular();
        var ifood = resultado.Canais.Single(canal => canal.Tipo == TipoDeCanal.IFood);

        // 8,00 / (1 - 0,06 - 0,27) = 11,9403 -> 11,95
        Assert.Equal(1195, ifood.PrecoMinimo.Centavos);
        Assert.Equal(1195, ifood.PrecoSugerido.Centavos);
        Assert.True(ifood.AjustadoPeloCustoTotal);
        Assert.True(ifood.PrecoSugerido > ifood.PrecoPorCmv);
        Assert.Equal(2510, ifood.CmvRealizado.PontosBase);
        Assert.True(ifood.MargemContribuicao > Dinheiro.Zero);
        Assert.Equal(0, ifood.LucroOperacional.Centavos);
    }

    [Fact]
    public void Rendimento_aumenta_o_custo_do_ingrediente()
    {
        var dados = new DadosDaFicha
        {
            NomeDoPrato = "Filé com fritas",
            RendimentoEmPorcoes = 10,
            PorcoesVendidasPorMes = 100,
            Ingredientes =
            [
                new Ingrediente("Filé", 1, "kg", Dinheiro.DeReais(20m), Percentual.De(80m))
            ],
            Embalagens = [],
            MinutosDeProducao = 0,
            ValorHoraMaoDeObra = Dinheiro.Zero,
            CustosFixosMensais = Dinheiro.Zero,
            Imposto = Percentual.Zero,
            CmvAlvo = Percentual.De(25m),
            Canais = [new CanalDeVenda(TipoDeCanal.Salao, Percentual.Zero, Dinheiro.Zero)],
            Arredondamento = Arredondamento.Nenhum
        };

        var resultado = Fabrica.Ficha(dados).Calcular();

        // 20,00 / 80% = 25,00 no lote; 2,50 por porção
        Assert.Equal(2500, resultado.CustoIngredientesLote.Centavos);
        Assert.Equal(250, resultado.CustoIngredientesPorcao.Centavos);
        Assert.Equal(1000, resultado.Canais[0].PrecoPorCmv.Centavos);
    }

    [Fact]
    public void Rateia_custo_fixo_pelas_porcoes_vendidas()
    {
        var resultado = Fabrica.Ficha(Fabrica.Dados(
            custoFixoMensal: 3000m,
            porcoesMes: 1000)).Calcular();

        Assert.Equal(300, resultado.CustoFixoPorcao.Centavos);
        Assert.Equal(1000, resultado.CustoTotalPorcao.Centavos);
    }

    [Fact]
    public void Arredonda_preco_minimo_para_cima()
    {
        var resultado = Fabrica.Ficha(Fabrica.Dados(
            arredondamento: Arredondamento.Real)).Calcular();
        var salao = resultado.Canais[0];

        Assert.Equal(900, salao.PrecoMinimo.Centavos);
        Assert.Equal(1000, salao.PrecoSugerido.Centavos);
    }

    [Fact]
    public void Taxa_fixa_por_pedido_entra_no_preco_minimo()
    {
        var canais = new[]
        {
            new CanalDeVenda(TipoDeCanal.IFood, Percentual.De(20m), Dinheiro.DeReais(2m))
        };

        var resultado = Fabrica.Ficha(Fabrica.Dados(canais: canais)).Calcular();
        var ifood = resultado.Canais[0];

        // (8,00 + 2,00) / (1 - 0,06 - 0,20) = 13,5135 -> 13,52
        Assert.Equal(1352, ifood.PrecoMinimo.Centavos);
        Assert.Equal(1352, ifood.PrecoSugerido.Centavos);
        Assert.Equal(200, ifood.TaxaFixaAplicada.Centavos);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(95)]
    public void Rejeita_cmv_fora_da_faixa(decimal cmv)
    {
        var excecao = Assert.Throws<DomainException>(() =>
            Fabrica.Ficha(Fabrica.Dados(cmv: cmv)));

        Assert.Equal("cmv_invalido", excecao.Codigo);
    }

    [Fact]
    public void Rejeita_soma_de_imposto_e_comissao_maior_que_cem()
    {
        var canais = new[]
        {
            new CanalDeVenda(TipoDeCanal.IFood, Percentual.De(95m), Dinheiro.Zero)
        };

        var excecao = Assert.Throws<DomainException>(() =>
            Fabrica.Ficha(Fabrica.Dados(imposto: 6m, canais: canais)));

        Assert.Equal("percentuais_acima_de_cem", excecao.Codigo);
    }

    [Fact]
    public void Rejeita_canal_duplicado()
    {
        var canais = new[]
        {
            new CanalDeVenda(TipoDeCanal.Salao, Percentual.Zero, Dinheiro.Zero),
            new CanalDeVenda(TipoDeCanal.Salao, Percentual.Zero, Dinheiro.Zero)
        };

        var excecao = Assert.Throws<DomainException>(() =>
            Fabrica.Ficha(Fabrica.Dados(canais: canais)));

        Assert.Equal("canal_duplicado", excecao.Codigo);
    }

    [Fact]
    public void Rejeita_ficha_sem_canais_ativos()
    {
        var canais = new[]
        {
            new CanalDeVenda(TipoDeCanal.Salao, Percentual.Zero, Dinheiro.Zero, ativo: false)
        };

        var excecao = Assert.Throws<DomainException>(() =>
            Fabrica.Ficha(Fabrica.Dados(canais: canais)));

        Assert.Equal("ficha_sem_canais_ativos", excecao.Codigo);
    }

    [Fact]
    public void Rejeita_ingrediente_com_rendimento_zero()
    {
        Assert.Throws<DomainException>(() =>
            new Ingrediente("Batata", 1, "kg", Dinheiro.DeReais(10m), Percentual.Zero));
    }

    [Fact]
    public void Rejeita_ficha_sem_custos()
    {
        var dados = new DadosDaFicha
        {
            NomeDoPrato = "Prato vazio",
            RendimentoEmPorcoes = 1,
            PorcoesVendidasPorMes = 1,
            Ingredientes = [new Ingrediente("Água", 1, "l", Dinheiro.Zero, Percentual.De(100m))],
            Embalagens = [],
            MinutosDeProducao = 0,
            ValorHoraMaoDeObra = Dinheiro.Zero,
            CustosFixosMensais = Dinheiro.Zero,
            CmvAlvo = Percentual.De(30m),
            Canais = [new CanalDeVenda(TipoDeCanal.Salao, Percentual.Zero, Dinheiro.Zero)]
        };

        var excecao = Assert.Throws<DomainException>(() => Fabrica.Ficha(dados));

        Assert.Equal("ficha_sem_custos", excecao.Codigo);
    }

    [Fact]
    public void Registra_evento_ao_criar_e_atualiza_preco()
    {
        var ficha = Fabrica.Ficha();
        var precoOriginal = ficha.Calcular().Canais[0].PrecoSugerido;

        Assert.Single(ficha.Eventos);

        var resultado = ficha.Atualizar(Fabrica.Dados(cmv: 25m), Fabrica.Agora.AddHours(1));

        Assert.True(resultado.Canais[0].PrecoSugerido > precoOriginal);
        Assert.Single(ficha.Eventos);
    }
}

using Precifica.Domain.Common;
using Precifica.Domain.Precificacao;

namespace Precifica.Domain.Tests;

public sealed class CalculoTests
{
    [Fact]
    public void Calcula_margem_sobre_o_preco_final()
    {
        var calculo = Fabrica.Calculo(Fabrica.Dados());

        var resultado = calculo.Calcular();

        Assert.Equal(1000, resultado.CustoDeMateriais.Centavos);
        Assert.Equal(6000, resultado.CustoDeMaoDeObra.Centavos);
        Assert.Equal(7000, resultado.CustoTotal.Centavos);
        Assert.Equal(8750, resultado.PrecoSugerido.Centavos);
        Assert.Equal(1750, resultado.LucroLiquido.Centavos);
        Assert.Equal(2000, resultado.MargemEfetiva.PontosBase);
    }

    [Fact]
    public void Impostos_e_taxa_do_cartao_incidem_sobre_o_preco()
    {
        var calculo = Fabrica.Calculo(Fabrica.Dados(imposto: 6m, taxaCartao: 5m));

        var resultado = calculo.Calcular();

        // 7000 / (1 - 0,20 - 0,06 - 0,05) = 10144,927... -> 10145
        Assert.Equal(10145, resultado.PrecoSugerido.Centavos);
        Assert.Equal(609, resultado.Impostos.Centavos);
        Assert.Equal(507, resultado.TaxaDoCartao.Centavos);
        Assert.Equal(2029, resultado.LucroLiquido.Centavos);
        Assert.Equal(2000, resultado.MargemEfetiva.PontosBase);
    }

    [Fact]
    public void Aplica_perda_de_material_e_rateia_custo_fixo()
    {
        var calculo = Fabrica.Calculo(Fabrica.Dados(
            perda: 10m,
            minutos: 30,
            custoFixoMensal: 300m,
            horasProdutivas: 100,
            margem: 30m));

        var resultado = calculo.Calcular();

        Assert.Equal(100, resultado.PerdaDeMaterial.Centavos);
        Assert.Equal(1100, resultado.CustoDeMateriais.Centavos);
        Assert.Equal(3000, resultado.CustoDeMaoDeObra.Centavos);
        Assert.Equal(150, resultado.CustoFixoRateado.Centavos);
        Assert.Equal(4250, resultado.CustoTotal.Centavos);
        Assert.Equal(6072, resultado.PrecoSugerido.Centavos);
    }

    [Fact]
    public void Calcula_preco_minimo_sem_margem()
    {
        var calculo = Fabrica.Calculo(Fabrica.Dados(imposto: 6m, taxaCartao: 5m));

        var resultado = calculo.Calcular();

        // 7000 / 0,89 = 7865,168... -> 7866
        Assert.Equal(7866, resultado.PrecoMinimo.Centavos);
        Assert.True(resultado.PrecoMinimo < resultado.PrecoSugerido);
    }

    [Fact]
    public void Arredondamento_e_preco_por_unidade()
    {
        var calculo = Fabrica.Calculo(Fabrica.Dados(
            imposto: 6m,
            taxaCartao: 5m,
            quantidade: 4,
            arredondamento: Arredondamento.Real));

        var resultado = calculo.Calcular();

        Assert.Equal(10200, resultado.PrecoSugerido.Centavos);
        Assert.Equal(2550, resultado.PrecoSugeridoPorUnidade.Centavos);
        Assert.Equal(7900, resultado.PrecoMinimo.Centavos);
        Assert.Equal(1975, resultado.PrecoMinimoPorUnidade.Centavos);
        Assert.Equal(1750, resultado.CustoTotalPorUnidade.Centavos);
    }

    [Fact]
    public void Taxa_fixa_por_venda_entra_no_preco()
    {
        var calculo = Fabrica.Calculo(Fabrica.Dados(taxaFixa: 1m, margem: 20m));

        var resultado = calculo.Calcular();

        // (7000 + 100) / 0,8 = 8875
        Assert.Equal(8875, resultado.PrecoSugerido.Centavos);
        Assert.Equal(100, resultado.TaxaFixa.Centavos);
        Assert.Equal(1775, resultado.LucroLiquido.Centavos);
    }

    [Fact]
    public void Rejeita_soma_de_percentuais_maior_ou_igual_a_cem()
    {
        var dados = Fabrica.Dados(margem: 90m, imposto: 6m, taxaCartao: 5m);

        var excecao = Assert.Throws<DomainException>(() => Fabrica.Calculo(dados));

        Assert.Equal("percentuais_acima_de_cem", excecao.Codigo);
    }

    [Fact]
    public void Rejeita_calculo_sem_nenhum_custo()
    {
        var dados = Fabrica.Dados(custoMaterial: 0m, minutos: 0, valorHora: 0m);

        var excecao = Assert.Throws<DomainException>(() => Fabrica.Calculo(dados));

        Assert.Equal("calculo_sem_custos", excecao.Codigo);
    }

    [Fact]
    public void Rejeita_quantidade_invalida()
    {
        var dados = Fabrica.Dados(quantidade: 0);

        var excecao = Assert.Throws<DomainException>(() => Fabrica.Calculo(dados));

        Assert.Equal("quantidade_invalida", excecao.Codigo);
    }

    [Fact]
    public void Rejeita_material_com_custo_negativo()
    {
        Assert.Throws<DomainException>(() =>
            new ItemDeMaterial("Chocolate", 1, "kg", new Dinheiro(-1)));
    }

    [Fact]
    public void Registra_evento_de_dominio_ao_criar()
    {
        var calculo = Fabrica.Calculo();

        var evento = Assert.Single(calculo.Eventos);
        Assert.IsType<Precifica.Domain.Precificacao.Eventos.CalculoRegistrado>(evento);
    }

    [Fact]
    public void Atualizacao_recalcula_e_preserva_identidade()
    {
        var calculo = Fabrica.Calculo();
        var precoOriginal = calculo.Calcular().PrecoSugerido;

        var resultado = calculo.Atualizar(Fabrica.Dados(margem: 30m), Fabrica.Agora.AddHours(1));

        Assert.Equal(calculo.Id, calculo.Id);
        Assert.True(resultado.PrecoSugerido > precoOriginal);
        Assert.Single(calculo.Eventos);
    }
}

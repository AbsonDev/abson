using Precifica.Domain.Common;
using Precifica.Domain.Precificacao;

namespace Precifica.Domain.Tests;

public sealed class DinheiroTests
{
    [Fact]
    public void Converte_reais_para_centavos_com_arredondamento_comercial()
    {
        Assert.Equal(1235, Dinheiro.DeReais(12.345m).Centavos);
        Assert.Equal(1000, Dinheiro.DeReais(10m).Centavos);
    }

    [Theory]
    [InlineData(10145, Arredondamento.Nenhum, 10145)]
    [InlineData(10145, Arredondamento.CinquentaCentavos, 10150)]
    [InlineData(10145, Arredondamento.Real, 10200)]
    [InlineData(10145, Arredondamento.CincoReais, 10500)]
    public void Arredonda_sempre_para_cima(long centavos, Arredondamento arredondamento, long esperado)
    {
        var resultado = arredondamento.ArredondarParaCima(new Dinheiro(centavos));

        Assert.Equal(esperado, resultado.Centavos);
    }

    [Fact]
    public void Percentual_preserva_centesimos()
    {
        var percentual = Percentual.De(4.99m);

        Assert.Equal(499, percentual.PontosBase);
        Assert.Equal(499, percentual.AplicarSobre(Dinheiro.DeReais(100m)).Centavos);
    }

    [Fact]
    public void Soma_e_multiplicacao_de_dinheiro_sao_exatas_em_centavos()
    {
        var dez = Dinheiro.DeReais(10m);
        var metade = dez.Multiplicar(0.5m);

        Assert.Equal(500, metade.Centavos);
        Assert.Equal(1500, (dez + metade).Centavos);
    }
}

using Precifica.Application.Calculos;
using Precifica.Domain.Precificacao;

namespace Precifica.Application.Tests;

public sealed class CalculoMapperTests
{
    [Fact]
    public void Mapeia_entrada_em_reais_para_value_objects_do_dominio()
    {
        var entrada = new EntradaDeCalculo
        {
            Nicho = Nicho.Confeitaria,
            Titulo = "Bolo",
            QuantidadeProduzida = 2,
            Materiais = [new EntradaDeMaterial("Chocolate", 0.3m, "kg", 32.00m)],
            CustosExtras = [new EntradaDeCustoExtra("Embalagem", 4.50m)],
            MinutosDeTrabalho = 120,
            ValorHora = 35m,
            MargemDesejadaPercentual = 40m,
            ImpostoPercentual = 6m,
            TaxaCartaoPercentual = 4.99m,
            Arredondamento = Arredondamento.Real
        };

        var dados = CalculoMapper.ParaDados(entrada);

        Assert.Equal(960, dados.Materiais[0].CustoTotal.Centavos);
        Assert.Equal(450, dados.CustosExtras[0].Valor.Centavos);
        Assert.Equal(600, dados.Imposto.PontosBase);
        Assert.Equal(499, dados.TaxaCartao.PontosBase);
        Assert.Equal(Arredondamento.Real, dados.Arredondamento);
    }

    [Fact]
    public void Dados_incompletos_viram_erro_de_dominio_amigavel()
    {
        var entrada = new EntradaDeCalculo { Titulo = "Bolo" };

        var excecao = Assert.Throws<Domain.Common.DomainException>(() =>
            Calculo.Criar(
                CalculoMapper.ParaDados(entrada),
                Guid.NewGuid(),
                "share",
                new string('a', 64),
                DateTimeOffset.UtcNow));

        Assert.Equal("calculo_sem_custos", excecao.Codigo);
    }
}

using Precifica.Domain.Common;
using Precifica.Domain.Precificacao;

namespace Precifica.Application.Calculos;

public sealed record CalculoSalvoDto(Guid Id, string ShareToken, string EditToken);

public sealed record CalculoDetalhadoDto(
    Guid Id,
    string ShareToken,
    string Titulo,
    Nicho Nicho,
    string NichoNome,
    int QuantidadeProduzida,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm,
    EntradaDeCalculo Entrada,
    ResultadoDto Resultado);

public sealed record ResultadoDto(
    decimal CustoDeMateriais,
    decimal PerdaDeMaterial,
    decimal CustoDeMaoDeObra,
    decimal CustoFixoRateado,
    decimal CustosExtras,
    decimal CustoTotal,
    decimal Impostos,
    decimal TaxaDoCartao,
    decimal TaxaFixa,
    decimal PrecoSugerido,
    decimal PrecoMinimo,
    decimal LucroLiquido,
    decimal MargemEfetivaPercentual,
    decimal PrecoSugeridoPorUnidade,
    decimal PrecoMinimoPorUnidade,
    decimal CustoTotalPorUnidade,
    int QuantidadeProduzida);

public static class CalculoMapper
{
    public static DadosDoCalculo ParaDados(EntradaDeCalculo entrada) =>
        new()
        {
            Nicho = entrada.Nicho,
            Titulo = entrada.Titulo ?? string.Empty,
            QuantidadeProduzida = entrada.QuantidadeProduzida,
            Materiais = (entrada.Materiais ?? [])
                .Select(material => new ItemDeMaterial(
                    material.Descricao ?? string.Empty,
                    material.Quantidade,
                    material.Unidade ?? string.Empty,
                    Dinheiro.DeReais(material.CustoUnitario)))
                .ToList(),
            CustosExtras = (entrada.CustosExtras ?? [])
                .Select(extra => new CustoExtra(
                    extra.Descricao ?? string.Empty,
                    Dinheiro.DeReais(extra.Valor)))
                .ToList(),
            PerdaDeMaterial = Percentual.De(entrada.PerdaDeMaterialPercentual),
            MinutosDeTrabalho = entrada.MinutosDeTrabalho,
            ValorHora = Dinheiro.DeReais(entrada.ValorHora),
            CustosFixosMensais = Dinheiro.DeReais(entrada.CustosFixosMensais),
            HorasProdutivasPorMes = entrada.HorasProdutivasPorMes,
            Imposto = Percentual.De(entrada.ImpostoPercentual),
            TaxaCartao = Percentual.De(entrada.TaxaCartaoPercentual),
            TaxaFixaPorVenda = Dinheiro.DeReais(entrada.TaxaFixaPorVenda),
            MargemDesejada = Percentual.De(entrada.MargemDesejadaPercentual),
            Arredondamento = entrada.Arredondamento
        };

    public static EntradaDeCalculo ParaEntrada(Calculo calculo) =>
        new()
        {
            Nicho = calculo.Nicho,
            Titulo = calculo.Titulo,
            QuantidadeProduzida = calculo.QuantidadeProduzida,
            Materiais = calculo.Materiais
                .Select(material => new EntradaDeMaterial(
                    material.Descricao,
                    material.Quantidade,
                    material.Unidade,
                    material.CustoUnitario.Reais))
                .ToList(),
            CustosExtras = calculo.CustosExtras
                .Select(extra => new EntradaDeCustoExtra(extra.Descricao, extra.Valor.Reais))
                .ToList(),
            PerdaDeMaterialPercentual = calculo.PerdaDeMaterial.Valor,
            MinutosDeTrabalho = calculo.MinutosDeTrabalho,
            ValorHora = calculo.ValorHora.Reais,
            CustosFixosMensais = calculo.CustosFixosMensais.Reais,
            HorasProdutivasPorMes = calculo.HorasProdutivasPorMes,
            ImpostoPercentual = calculo.Imposto.Valor,
            TaxaCartaoPercentual = calculo.TaxaCartao.Valor,
            TaxaFixaPorVenda = calculo.TaxaFixaPorVenda.Reais,
            MargemDesejadaPercentual = calculo.MargemDesejada.Valor,
            Arredondamento = calculo.Arredondamento
        };

    public static ResultadoDto ParaDto(ResultadoDaPrecificacao resultado) =>
        new(
            resultado.CustoDeMateriais.Reais,
            resultado.PerdaDeMaterial.Reais,
            resultado.CustoDeMaoDeObra.Reais,
            resultado.CustoFixoRateado.Reais,
            resultado.CustosExtras.Reais,
            resultado.CustoTotal.Reais,
            resultado.Impostos.Reais,
            resultado.TaxaDoCartao.Reais,
            resultado.TaxaFixa.Reais,
            resultado.PrecoSugerido.Reais,
            resultado.PrecoMinimo.Reais,
            resultado.LucroLiquido.Reais,
            resultado.MargemEfetiva.Valor,
            resultado.PrecoSugeridoPorUnidade.Reais,
            resultado.PrecoMinimoPorUnidade.Reais,
            resultado.CustoTotalPorUnidade.Reais,
            resultado.QuantidadeProduzida);
}

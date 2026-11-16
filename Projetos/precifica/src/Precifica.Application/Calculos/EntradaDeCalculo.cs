using Precifica.Domain.Precificacao;

namespace Precifica.Application.Calculos;

/// <summary>
/// Contrato de entrada dos casos de uso de cálculo. Fica em reais/percentuais
/// porque é o formato que a UI e o JSON usam; a conversão para value objects
/// acontece no mapper.
/// </summary>
public sealed record EntradaDeCalculo
{
    public Nicho Nicho { get; init; } = Nicho.Confeitaria;

    public string? Titulo { get; init; }

    public int QuantidadeProduzida { get; init; } = 1;

    public IReadOnlyList<EntradaDeMaterial>? Materiais { get; init; }

    public IReadOnlyList<EntradaDeCustoExtra>? CustosExtras { get; init; }

    public decimal PerdaDeMaterialPercentual { get; init; }

    public int MinutosDeTrabalho { get; init; }

    public decimal ValorHora { get; init; }

    public decimal CustosFixosMensais { get; init; }

    public int HorasProdutivasPorMes { get; init; } = 160;

    public decimal ImpostoPercentual { get; init; }

    public decimal TaxaCartaoPercentual { get; init; }

    public decimal TaxaFixaPorVenda { get; init; }

    public decimal MargemDesejadaPercentual { get; init; }

    public Arredondamento Arredondamento { get; init; } = Arredondamento.Nenhum;
}

public sealed record EntradaDeMaterial(
    string? Descricao,
    decimal Quantidade,
    string? Unidade,
    decimal CustoUnitario);

public sealed record EntradaDeCustoExtra(string? Descricao, decimal Valor);

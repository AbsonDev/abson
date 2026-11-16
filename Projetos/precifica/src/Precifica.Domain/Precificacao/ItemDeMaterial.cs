using Precifica.Domain.Common;

namespace Precifica.Domain.Precificacao;

/// <summary>
/// Linha de material: quantidade usada x custo por unidade de medida.
/// Exemplo: 0,3 kg de chocolate a R$ 32,00/kg.
/// </summary>
public sealed class ItemDeMaterial
{
    private ItemDeMaterial()
    {
    }

    public ItemDeMaterial(string descricao, decimal quantidade, string unidade, Dinheiro custoUnitario)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            throw new DomainException("material_sem_descricao", "Todo material precisa de uma descrição.");
        }

        if (quantidade <= 0 || quantidade > 1_000_000m)
        {
            throw new DomainException(
                "material_quantidade_invalida",
                $"A quantidade de \"{descricao}\" precisa estar entre 0 e 1.000.000.");
        }

        if (string.IsNullOrWhiteSpace(unidade))
        {
            throw new DomainException("material_sem_unidade", $"Informe a unidade de medida de \"{descricao}\".");
        }

        if (custoUnitario.EhNegativo || custoUnitario.Centavos > 100_000_000)
        {
            throw new DomainException(
                "material_custo_invalido",
                $"O custo de \"{descricao}\" precisa estar entre R$ 0,00 e R$ 1.000.000,00.");
        }

        Descricao = descricao.Trim();
        Quantidade = quantidade;
        Unidade = unidade.Trim().ToLowerInvariant();
        CustoUnitario = custoUnitario;
    }

    public string Descricao { get; private set; } = string.Empty;

    public decimal Quantidade { get; private set; }

    public string Unidade { get; private set; } = string.Empty;

    public Dinheiro CustoUnitario { get; private set; }

    public Dinheiro CustoTotal => CustoUnitario.Multiplicar(Quantidade);
}

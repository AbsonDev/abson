using CardapioNoLucro.Domain.Common;

namespace CardapioNoLucro.Domain.Fichas;

/// <summary>
/// Insumo da ficha técnica. O rendimento representa o aproveitamento após
/// limpeza/corte: 80% significa que 20% do que foi comprado é perdido.
/// </summary>
public sealed class Ingrediente
{
    private Ingrediente()
    {
    }

    public Ingrediente(
        string descricao,
        decimal quantidade,
        string unidade,
        Dinheiro precoUnitario,
        Percentual rendimento)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            throw new DomainException("ingrediente_sem_descricao", "Todo ingrediente precisa de uma descrição.");
        }

        if (quantidade <= 0 || quantidade > 1_000_000m)
        {
            throw new DomainException(
                "ingrediente_quantidade_invalida",
                $"A quantidade de \"{descricao}\" precisa estar entre 0 e 1.000.000.");
        }

        if (string.IsNullOrWhiteSpace(unidade))
        {
            throw new DomainException("ingrediente_sem_unidade", $"Informe a unidade de \"{descricao}\".");
        }

        if (precoUnitario.EhNegativo || precoUnitario.Centavos > 100_000_000)
        {
            throw new DomainException(
                "ingrediente_preco_invalido",
                $"O preço de \"{descricao}\" precisa estar entre R$ 0,00 e R$ 1.000.000,00.");
        }

        if (rendimento.Valor <= 0 || rendimento.Valor > 100)
        {
            throw new DomainException(
                "ingrediente_rendimento_invalido",
                $"O rendimento de \"{descricao}\" deve estar entre 0,01% e 100%.");
        }

        Descricao = descricao.Trim();
        Quantidade = quantidade;
        Unidade = unidade.Trim().ToLowerInvariant();
        PrecoUnitario = precoUnitario;
        Rendimento = rendimento;
    }

    public string Descricao { get; private set; } = string.Empty;

    public decimal Quantidade { get; private set; }

    public string Unidade { get; private set; } = string.Empty;

    public Dinheiro PrecoUnitario { get; private set; }

    public Percentual Rendimento { get; private set; }

    public Dinheiro CustoBruto => PrecoUnitario.Multiplicar(Quantidade);

    /// <summary>Custo real considerando a perda de limpeza/corte.</summary>
    public Dinheiro CustoComRendimento => CustoBruto.Multiplicar(100m / Rendimento.Valor);
}

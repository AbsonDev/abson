using Precifica.Domain.Common;

namespace Precifica.Domain.Precificacao;

/// <summary>
/// Custo direto que não é material nem mão de obra: embalagem, entrega,
/// plataforma, aluguel de equipamento etc.
/// </summary>
public sealed class CustoExtra
{
    private CustoExtra()
    {
    }

    public CustoExtra(string descricao, Dinheiro valor)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            throw new DomainException("custo_extra_sem_descricao", "Todo custo extra precisa de uma descrição.");
        }

        if (valor.EhNegativo || valor.Centavos > 100_000_000)
        {
            throw new DomainException(
                "custo_extra_invalido",
                $"O custo extra \"{descricao}\" precisa estar entre R$ 0,00 e R$ 1.000.000,00.");
        }

        Descricao = descricao.Trim();
        Valor = valor;
    }

    public string Descricao { get; private set; } = string.Empty;

    public Dinheiro Valor { get; private set; }
}

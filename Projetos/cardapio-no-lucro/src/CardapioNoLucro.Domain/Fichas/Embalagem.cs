using CardapioNoLucro.Domain.Common;

namespace CardapioNoLucro.Domain.Fichas;

/// <summary>
/// Embalagem ou descartável consumido por porção (marmita, sacola, copo etc.).
/// </summary>
public sealed class Embalagem
{
    private Embalagem()
    {
    }

    public Embalagem(string descricao, Dinheiro custoPorPorcao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            throw new DomainException("embalagem_sem_descricao", "Toda embalagem precisa de uma descrição.");
        }

        if (custoPorPorcao.EhNegativo || custoPorPorcao.Centavos > 100_000_000)
        {
            throw new DomainException(
                "embalagem_custo_invalido",
                $"O custo de \"{descricao}\" precisa estar entre R$ 0,00 e R$ 1.000.000,00.");
        }

        Descricao = descricao.Trim();
        CustoPorPorcao = custoPorPorcao;
    }

    public string Descricao { get; private set; } = string.Empty;

    public Dinheiro CustoPorPorcao { get; private set; }
}

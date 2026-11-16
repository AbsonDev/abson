using CardapioNoLucro.Domain.Common;

namespace CardapioNoLucro.Domain.Fichas;

/// <summary>
/// Configuração de venda de um canal (salão, delivery próprio, iFood etc.).
/// Percentual e taxa fixa incidem sobre o preço de venda.
/// </summary>
public sealed class CanalDeVenda
{
    private CanalDeVenda()
    {
    }

    public CanalDeVenda(
        TipoDeCanal tipo,
        Percentual comissao,
        Dinheiro taxaFixaPorPedido,
        bool ativo = true)
    {
        if (!Enum.IsDefined(tipo))
        {
            throw new DomainException("canal_invalido", "Escolha um canal de venda válido.");
        }

        if (comissao.Valor < 0 || comissao.Valor >= 100)
        {
            throw new DomainException("comissao_invalida", "A comissão do canal deve estar entre 0% e 99,99%.");
        }

        if (taxaFixaPorPedido.EhNegativo || taxaFixaPorPedido.Centavos > 100_000_000)
        {
            throw new DomainException(
                "taxa_fixa_invalida",
                "A taxa fixa por pedido deve estar entre R$ 0,00 e R$ 1.000.000,00.");
        }

        Tipo = tipo;
        Comissao = comissao;
        TaxaFixaPorPedido = taxaFixaPorPedido;
        Ativo = ativo;
    }

    public TipoDeCanal Tipo { get; private set; }

    public Percentual Comissao { get; private set; }

    public Dinheiro TaxaFixaPorPedido { get; private set; }

    public bool Ativo { get; private set; }
}

using Precifica.Domain.Common;
using Precifica.Domain.Pagamentos;

namespace Precifica.Domain.Tests;

public sealed class PedidoTests
{
    [Fact]
    public void Normaliza_email_e_comeca_pendente()
    {
        var pedido = Pedido.CriarPdfAvulso(
            Guid.NewGuid(),
            "  Cliente@Exemplo.COM ",
            Dinheiro.DeReais(29m),
            Fabrica.Agora);

        Assert.Equal("cliente@exemplo.com", pedido.Email);
        Assert.Equal(StatusDoPedido.Pendente, pedido.Status);
        Assert.Equal(2900, pedido.Valor.Centavos);
    }

    [Fact]
    public void Confirmacao_de_pagamento_e_idempotente()
    {
        var pedido = Fabrica.Pedido();

        var primeira = pedido.ConfirmarPagamento("mp-123", Dinheiro.DeReais(29m), Fabrica.Agora);
        var segunda = pedido.ConfirmarPagamento("mp-123", Dinheiro.DeReais(29m), Fabrica.Agora);

        Assert.True(primeira);
        Assert.False(segunda);
        Assert.Equal(StatusDoPedido.Pago, pedido.Status);
        Assert.Single(pedido.Eventos);
    }

    [Fact]
    public void Rejeita_pagamento_com_valor_menor()
    {
        var pedido = Fabrica.Pedido();

        var excecao = Assert.Throws<DomainException>(() =>
            pedido.ConfirmarPagamento("mp-123", Dinheiro.DeReais(20m), Fabrica.Agora));

        Assert.Equal("pagamento_valor_divergente", excecao.Codigo);
    }

    [Fact]
    public void Rejeita_email_invalido()
    {
        var excecao = Assert.Throws<DomainException>(() =>
            Pedido.CriarPdfAvulso(Guid.NewGuid(), "sem-arroba", Dinheiro.DeReais(29m), Fabrica.Agora));

        Assert.Equal("email_invalido", excecao.Codigo);
    }
}

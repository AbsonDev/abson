using Microsoft.Extensions.Options;
using Precifica.Application.Pagamentos;
using Precifica.Domain.Common;
using Precifica.Domain.Pagamentos;

namespace Precifica.Application.Tests;

public sealed class ServicoDeAcessoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 15, 12, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public async Task Compra_avulsa_libera_o_calculo()
    {
        var pedidos = new PedidoRepositorioFake();
        var assinaturas = new AssinaturaRepositorioFake();
        var calculoId = Guid.NewGuid();
        var pedido = Pedido.CriarPdfAvulso(calculoId, "cliente@exemplo.com", Dinheiro.DeReais(29m), Agora);
        pedido.ConfirmarPagamento("mp-1", Dinheiro.DeReais(29m), Agora);
        await pedidos.AdicionarAsync(pedido);

        var servico = new ServicoDeAcesso(pedidos, assinaturas, new RelogioFixo(Agora), Options.Create(new MonetizacaoOptions { Ativa = true }));
        var acesso = await servico.ObterAsync(calculoId);

        Assert.True(acesso.PdfAvulsoPago);
        Assert.True(acesso.Premium);
    }

    [Fact]
    public async Task Assinatura_ativa_libera_por_email()
    {
        var pedidos = new PedidoRepositorioFake();
        var assinaturas = new AssinaturaRepositorioFake();
        var assinatura = Assinatura.Criar("cliente@exemplo.com", Dinheiro.DeReais(9m), Agora);
        assinatura.Ativar("preapproval-1", Agora);
        await assinaturas.AdicionarAsync(assinatura);

        var servico = new ServicoDeAcesso(pedidos, assinaturas, new RelogioFixo(Agora), Options.Create(new MonetizacaoOptions { Ativa = true }));
        var acesso = await servico.ObterAsync(Guid.NewGuid(), " CLIENTE@EXEMPLO.COM ");

        Assert.True(acesso.AssinaturaAtiva);
        Assert.True(acesso.Premium);
    }

    [Fact]
    public async Task Assinatura_expirada_nao_libera()
    {
        var pedidos = new PedidoRepositorioFake();
        var assinaturas = new AssinaturaRepositorioFake();
        var assinatura = Assinatura.Criar("cliente@exemplo.com", Dinheiro.DeReais(9m), Agora);
        assinatura.Ativar("preapproval-1", Agora);
        await assinaturas.AdicionarAsync(assinatura);

        var servico = new ServicoDeAcesso(
            pedidos,
            assinaturas,
            new RelogioFixo(Agora.AddMonths(2)),
            Options.Create(new MonetizacaoOptions { Ativa = true }));

        var acesso = await servico.ObterAsync(Guid.NewGuid(), "cliente@exemplo.com");

        Assert.False(acesso.AssinaturaAtiva);
        Assert.False(acesso.Premium);
    }

    [Fact]
    public async Task Sem_compra_e_sem_email_nao_ha_acesso_premium()
    {
        var servico = new ServicoDeAcesso(
            new PedidoRepositorioFake(),
            new AssinaturaRepositorioFake(),
            new RelogioFixo(Agora), Options.Create(new MonetizacaoOptions { Ativa = true }));

        var acesso = await servico.ObterAsync(Guid.NewGuid());

        Assert.False(acesso.Premium);
    }

    [Fact]
    public async Task Modo_gratuito_libera_pdf_sem_marca_para_todos()
    {
        var servico = new ServicoDeAcesso(
            new PedidoRepositorioFake(),
            new AssinaturaRepositorioFake(),
            new RelogioFixo(Agora),
            Options.Create(new MonetizacaoOptions { Ativa = false }));

        var acesso = await servico.ObterAsync(Guid.NewGuid());

        Assert.True(acesso.Premium);
        Assert.False(acesso.PdfAvulsoPago);
        Assert.False(acesso.AssinaturaAtiva);
    }
}

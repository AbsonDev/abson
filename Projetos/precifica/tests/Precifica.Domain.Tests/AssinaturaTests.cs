using Precifica.Domain.Common;
using Precifica.Domain.Pagamentos;

namespace Precifica.Domain.Tests;

public sealed class AssinaturaTests
{
    [Fact]
    public void Ativa_por_um_mes_e_nao_encurta_periodo_em_notificacao_repetida()
    {
        var assinatura = Assinatura.Criar("cliente@exemplo.com", Dinheiro.DeReais(9m), Fabrica.Agora);

        assinatura.Ativar("preapproval-1", Fabrica.Agora);
        var fim = assinatura.PeriodoAtualFim;
        assinatura.Ativar("preapproval-1", Fabrica.Agora.AddDays(2));

        Assert.Equal(StatusDaAssinatura.Ativa, assinatura.Status);
        Assert.Equal(fim, assinatura.PeriodoAtualFim);
        Assert.True(assinatura.EstaAtiva(Fabrica.Agora.AddDays(10)));
        Assert.Single(assinatura.Eventos);
    }

    [Fact]
    public void Expiracao_do_periodo_remove_acesso()
    {
        var assinatura = Assinatura.Criar("cliente@exemplo.com", Dinheiro.DeReais(9m), Fabrica.Agora);
        assinatura.Ativar("preapproval-1", Fabrica.Agora);

        Assert.False(assinatura.EstaAtiva(Fabrica.Agora.AddMonths(2)));
    }

    [Fact]
    public void Cancelamento_encerra_acesso()
    {
        var assinatura = Assinatura.Criar("cliente@exemplo.com", Dinheiro.DeReais(9m), Fabrica.Agora);
        assinatura.Ativar("preapproval-1", Fabrica.Agora);

        assinatura.Cancelar(Fabrica.Agora.AddDays(5));

        Assert.Equal(StatusDaAssinatura.Cancelada, assinatura.Status);
        Assert.False(assinatura.EstaAtiva(Fabrica.Agora.AddDays(6)));
    }

    [Fact]
    public void Rejeita_valor_mensal_invalido()
    {
        var excecao = Assert.Throws<DomainException>(() =>
            Assinatura.Criar("cliente@exemplo.com", Dinheiro.Zero, Fabrica.Agora));

        Assert.Equal("assinatura_valor_invalido", excecao.Codigo);
    }
}

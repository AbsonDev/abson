using Fisga.Domain.Common;
using Fisga.Domain.Identidade;

namespace Fisga.Domain.Tests;

public sealed class SessaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Criar_com_id_informado_preserva_o_identificador()
    {
        var id = Guid.NewGuid();

        var sessao = Sessao.Criar(
            id,
            Guid.NewGuid(),
            new string('a', 64),
            "Cliente-A",
            null,
            Agora,
            TimeSpan.FromDays(30));

        Assert.Equal(id, sessao.Id);
        Assert.Equal(Agora, sessao.CriadoEm);
        Assert.Equal(Agora.AddDays(30), sessao.ExpiraEm);
        Assert.Equal("Cliente-A", sessao.UserAgent);
    }

    [Fact]
    public void Criar_sem_id_gera_identificador_não_vazio()
    {
        var sessao = Sessao.Criar(
            Guid.NewGuid(),
            new string('b', 64),
            null,
            null,
            Agora,
            TimeSpan.FromDays(1));

        Assert.NotEqual(Guid.Empty, sessao.Id);
    }

    [Fact]
    public void Criar_com_id_vazio_lanca_domain_exception()
    {
        var excecao = Assert.Throws<DomainException>(() => Sessao.Criar(
            Guid.Empty,
            Guid.NewGuid(),
            new string('a', 64),
            null,
            null,
            Agora,
            TimeSpan.FromDays(1)));

        Assert.Equal("sessao_sem_id", excecao.Codigo);
    }

    [Fact]
    public void Criar_com_token_invalido_lanca_domain_exception()
    {
        var excecao = Assert.Throws<DomainException>(() => Sessao.Criar(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "curto",
            null,
            null,
            Agora,
            TimeSpan.FromDays(1)));

        Assert.Equal("sessao_sem_token", excecao.Codigo);
    }

    [Fact]
    public void Criar_com_duracao_invalida_lanca_domain_exception()
    {
        var excecao = Assert.Throws<DomainException>(() => Sessao.Criar(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new string('a', 64),
            null,
            null,
            Agora,
            TimeSpan.Zero));

        Assert.Equal("sessao_duracao_invalida", excecao.Codigo);
    }
}

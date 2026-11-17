using Fisga.Domain.Common;
using Fisga.Domain.Pontos;

namespace Fisga.Domain.Tests;

public sealed class PontoPrivadoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 8, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void Cria_ponto_com_notas_e_tipo()
    {
        var ponto = new PontoPrivado(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Laje do Farol",
            "Pedra a 8 m, corrico de tucunaré.",
            -10.911,
            -37.071,
            TipoDePonto.Estrutura,
            Agora);

        Assert.Equal("Laje do Farol", ponto.Nome);
        Assert.Equal("Pedra a 8 m, corrico de tucunaré.", ponto.Descricao);
        Assert.Equal(TipoDePonto.Estrutura, ponto.Tipo);
        Assert.Equal(Agora, ponto.CriadoEm);
    }

    [Fact]
    public void Rejeita_coordenadas_invalidas()
    {
        var excecao = Assert.Throws<DomainException>(() =>
            new PontoPrivado(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Ponto",
                null,
                200,
                -37,
                TipoDePonto.Ponto,
                Agora));

        Assert.Equal("ponto_latitude_invalida", excecao.Codigo);
    }

    [Fact]
    public void Atualiza_dados_e_preserva_criacao()
    {
        var ponto = new PontoPrivado(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Poita",
            null,
            -10.9,
            -37.0,
            TipoDePonto.Poita,
            Agora);

        ponto.Atualizar(
            "Poita nova",
            "Fundo de areia.",
            -11.0,
            -37.2,
            TipoDePonto.Ponto,
            Agora.AddHours(2));

        Assert.Equal("Poita nova", ponto.Nome);
        Assert.Equal(TipoDePonto.Ponto, ponto.Tipo);
        Assert.Equal(Agora, ponto.CriadoEm);
        Assert.Equal(Agora.AddHours(2), ponto.AtualizadoEm);
    }
}

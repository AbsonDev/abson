using Fisga.Domain.Common;
using Fisga.Domain.Trilhas;

namespace Fisga.Domain.Tests;

public sealed class TrilhaPrivadaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 8, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void Cria_trilha_e_calcula_distancia()
    {
        var trilha = new TrilhaPrivada(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Corrico da laje",
            "Borda do canal.",
            TipoDeTrilha.Corrico,
            [
                new Coordenada(-10.911, -37.071),
                new Coordenada(-10.921, -37.081)
            ],
            Agora);

        Assert.Equal(2, trilha.Pontos.Count);
        Assert.True(trilha.DistanciaKm > 1);
        Assert.Equal("Corrico da laje", trilha.Nome);
    }

    [Fact]
    public void Rejeita_trilha_com_um_ponto()
    {
        var excecao = Assert.Throws<DomainException>(() =>
            new TrilhaPrivada(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Curta",
                null,
                TipoDeTrilha.Deslocamento,
                [new Coordenada(-10.9, -37.0)],
                Agora));

        Assert.Equal("trilha_curta", excecao.Codigo);
    }

    [Fact]
    public void Rejeita_coordenada_invalida()
    {
        var excecao = Assert.Throws<DomainException>(() =>
            new TrilhaPrivada(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Inválida",
                null,
                TipoDeTrilha.Corrico,
                [new Coordenada(-10.9, -37.0), new Coordenada(200, -37.0)],
                Agora));

        Assert.Equal("trilha_coordenada_invalida", excecao.Codigo);
    }
}

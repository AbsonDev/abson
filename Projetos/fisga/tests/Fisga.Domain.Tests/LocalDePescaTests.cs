using Fisga.Domain.Common;
using Fisga.Domain.Locais;
using Fisga.Domain.Pescarias;

namespace Fisga.Domain.Tests;

public sealed class LocalDePescaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 8, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void Cria_local_com_cidade_e_uf_normalizadas()
    {
        var local = new LocalDePesca(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "  Prainha do Farol ",
            " Aracaju ",
            "se",
            -10.911,
            -37.071,
            AmbienteDePesca.Mar,
            Agora);

        Assert.Equal("Prainha do Farol", local.Nome);
        Assert.Equal("Aracaju", local.Cidade);
        Assert.Equal("SE", local.Estado);
        Assert.Equal("Aracaju · SE", local.Localizacao);
        Assert.False(local.Padrao);
    }

    [Fact]
    public void Rejeita_coordenadas_fora_do_brasil()
    {
        var excecao = Assert.Throws<DomainException>(() =>
            new LocalDePesca(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Local",
                null,
                null,
                120,
                -37,
                AmbienteDePesca.Rio,
                Agora));

        Assert.Equal("local_latitude_invalida", excecao.Codigo);
    }

    [Fact]
    public void Rejeita_uf_com_mais_de_duas_letras()
    {
        var excecao = Assert.Throws<DomainException>(() =>
            new LocalDePesca(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Local",
                null,
                "Sergipe",
                -10.9,
                -37.0,
                AmbienteDePesca.Mar,
                Agora));

        Assert.Equal("local_uf_invalida", excecao.Codigo);
    }

    [Fact]
    public void Atualiza_dados_e_padrao()
    {
        var local = new LocalDePesca(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Local",
            "Aracaju",
            "SE",
            -10.9,
            -37.0,
            AmbienteDePesca.Mar,
            Agora);

        local.Atualizar("Novo nome", "Barra", "BA", -13.0, -38.5, AmbienteDePesca.Mar, Agora.AddHours(1));
        local.DefinirPadrao(true, Agora.AddHours(2));

        Assert.Equal("Novo nome", local.Nome);
        Assert.Equal("Barra", local.Cidade);
        Assert.Equal("BA", local.Estado);
        Assert.True(local.Padrao);
        Assert.Equal(Agora.AddHours(2), local.AtualizadoEm);
    }
}

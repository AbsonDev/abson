using Fisga.Domain.Common;
using Fisga.Domain.Pescarias;

namespace Fisga.Domain.Tests;

public sealed class PescariaTests
{
    [Fact]
    public void Cria_pescaria_com_capturas_e_evento()
    {
        var pescaria = Fabrica.Pescaria();

        Assert.Equal(2, pescaria.Capturas.Count);
        Assert.Equal("Represa do Lago Azul", pescaria.Local);
        Assert.Equal("SP", pescaria.Estado);
        Assert.Single(pescaria.Eventos);
    }

    [Fact]
    public void Atualizacao_preserva_ids_das_capturas_existentes()
    {
        var pescaria = Fabrica.Pescaria();
        var tucunare = pescaria.Capturas.First(captura => captura.Especie == "Tucunaré");
        var idOriginal = tucunare.Id;

        var dados = Fabrica.Dados(
            capturas:
            [
                new DadosDaCaptura(idOriginal, "Tucunaré", 3.1m, 52m, "Popper", true, null, "Cresceu!"),
                new DadosDaCaptura(null, "Pacu", 1.5m, 35m, "Milho", false, null, null)
            ]);

        pescaria.Atualizar(dados, Fabrica.Agora.AddHours(2));

        Assert.Equal(2, pescaria.Capturas.Count);
        Assert.Contains(pescaria.Capturas, captura => captura.Id == idOriginal && captura.PesoKg == 3.1m);
        Assert.Contains(pescaria.Capturas, captura => captura.Especie == "Pacu");
        Assert.DoesNotContain(pescaria.Capturas, captura => captura.Especie == "Tilápia");
    }

    [Fact]
    public void Rejeita_pescaria_sem_usuario()
    {
        var excecao = Assert.Throws<DomainException>(() =>
            Fisga.Domain.Pescarias.Pescaria.Criar(
                Fabrica.Dados(),
                Guid.NewGuid(),
                Guid.Empty,
                "share-token",
                Fabrica.Agora));

        Assert.Equal("pescaria_sem_usuario", excecao.Codigo);
    }

    [Fact]
    public void Reconhece_o_dono_da_pescaria()
    {
        var usuarioId = Guid.NewGuid();
        var pescaria = Fisga.Domain.Pescarias.Pescaria.Criar(
            Fabrica.Dados(),
            Guid.NewGuid(),
            usuarioId,
            "share-token",
            Fabrica.Agora);

        Assert.True(pescaria.PertenceA(usuarioId));
        Assert.False(pescaria.PertenceA(Guid.NewGuid()));
        Assert.False(pescaria.PertenceA(Guid.Empty));
    }

    [Fact]
    public void Rejeita_local_vazio()
    {
        var excecao = Assert.Throws<DomainException>(() =>
            Fabrica.Pescaria(Fabrica.Dados(local: " ")));

        Assert.Equal("pescaria_sem_local", excecao.Codigo);
    }

    [Fact]
    public void Rejeita_captura_sem_peso_nem_comprimento()
    {
        var excecao = Assert.Throws<DomainException>(() =>
            new Captura(Guid.NewGuid(), "Tucunaré", 0m, 0m, null, true, null, null));

        Assert.Equal("captura_sem_medida", excecao.Codigo);
    }

    [Fact]
    public void Rejeita_foto_fora_da_midia_do_fisga()
    {
        var excecao = Assert.Throws<DomainException>(() =>
            new Captura(Guid.NewGuid(), "Tucunaré", 2m, 45m, null, true, "https://exemplo.com/foto.jpg", null));

        Assert.Equal("foto_url_invalida", excecao.Codigo);
    }

    [Fact]
    public void Rejeita_captura_de_outra_pescaria()
    {
        var dados = Fabrica.Dados(
            capturas:
            [
                new DadosDaCaptura(Guid.NewGuid(), "Tucunaré", 2m, 45m, null, true, null, null)
            ]);

        var excecao = Assert.Throws<DomainException>(() => Fabrica.Pescaria(dados));

        Assert.Equal("captura_inexistente", excecao.Codigo);
    }

    [Fact]
    public void Permite_pescaria_sem_captura()
    {
        var pescaria = Fabrica.Pescaria(Fabrica.Dados() with { Capturas = [] });

        Assert.Empty(pescaria.Capturas);
        Assert.Single(pescaria.Eventos);
    }

    [Fact]
    public void Rejeita_captura_duplicada_na_atualizacao()
    {
        var pescaria = Fabrica.Pescaria();
        var primeira = pescaria.Capturas.First().Id;
        var dados = Fabrica.Dados(
            capturas:
            [
                new DadosDaCaptura(primeira, "Tucunaré", 2m, 45m, null, true, null, null),
                new DadosDaCaptura(primeira, "Tucunaré", 2.1m, 46m, null, true, null, null)
            ]);

        var excecao = Assert.Throws<DomainException>(() =>
            pescaria.Atualizar(dados, Fabrica.Agora));

        Assert.Equal("captura_duplicada", excecao.Codigo);
    }
}

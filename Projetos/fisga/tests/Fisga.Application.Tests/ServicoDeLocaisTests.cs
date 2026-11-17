using Fisga.Application.Abstractions;
using Fisga.Application.Locais;
using Fisga.Domain.Common;
using Fisga.Domain.Locais;

namespace Fisga.Application.Tests;

public sealed class ServicoDeLocaisTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 8, 0, 0, TimeSpan.FromHours(-3));

    private static EntradaDeLocal Entrada(string nome = "Aracaju", bool padrao = false) =>
        new(nome, "Aracaju", "SE", -10.911, -37.071, Domain.Pescarias.AmbienteDePesca.Mar, padrao);

    [Fact]
    public async Task Primeiro_local_vira_padrao()
    {
        var repositorio = new LocalDePescaRepositorioFake();
        var servico = new ServicoDeLocais(repositorio, new RelogioFixo(Agora));
        var usuarioId = Guid.NewGuid();

        var local = await servico.CriarAsync(Entrada(), usuarioId);

        Assert.True(local.Padrao);
        Assert.Single(repositorio.Locais);
    }

    [Fact]
    public async Task Limita_cinco_locais_por_usuario()
    {
        var repositorio = new LocalDePescaRepositorioFake();
        var servico = new ServicoDeLocais(repositorio, new RelogioFixo(Agora));
        var usuarioId = Guid.NewGuid();

        for (var indice = 0; indice < ServicoDeLocais.LimitePorUsuario; indice++)
        {
            await servico.CriarAsync(Entrada($"Local {indice}"), usuarioId);
        }

        var excecao = await Assert.ThrowsAsync<DomainException>(() =>
            servico.CriarAsync(Entrada("Sexto"), usuarioId));

        Assert.Equal("limite_de_locais", excecao.Codigo);
    }

    [Fact]
    public async Task Define_apenas_um_padrao()
    {
        var repositorio = new LocalDePescaRepositorioFake();
        var servico = new ServicoDeLocais(repositorio, new RelogioFixo(Agora));
        var usuarioId = Guid.NewGuid();

        var primeiro = await servico.CriarAsync(Entrada("Primeiro"), usuarioId);
        var segundo = await servico.CriarAsync(Entrada("Segundo"), usuarioId);
        await servico.DefinirPadraoAsync(segundo.Id, usuarioId);

        Assert.False(repositorio.Locais.Single(local => local.Id == primeiro.Id).Padrao);
        Assert.True(repositorio.Locais.Single(local => local.Id == segundo.Id).Padrao);
    }

    [Fact]
    public async Task Excluir_padrao_promove_outro_local()
    {
        var repositorio = new LocalDePescaRepositorioFake();
        var servico = new ServicoDeLocais(repositorio, new RelogioFixo(Agora));
        var usuarioId = Guid.NewGuid();

        var primeiro = await servico.CriarAsync(Entrada("Primeiro"), usuarioId);
        var segundo = await servico.CriarAsync(Entrada("Segundo"), usuarioId);

        await servico.ExcluirAsync(primeiro.Id, usuarioId);

        Assert.Single(repositorio.Locais);
        Assert.Equal(segundo.Id, repositorio.Locais[0].Id);
        Assert.True(repositorio.Locais[0].Padrao);
    }

    [Fact]
    public async Task Nao_permite_editar_local_de_outra_conta()
    {
        var repositorio = new LocalDePescaRepositorioFake();
        var servico = new ServicoDeLocais(repositorio, new RelogioFixo(Agora));
        var dono = Guid.NewGuid();
        var local = await servico.CriarAsync(Entrada(), dono);

        await Assert.ThrowsAsync<AcessoNegadoException>(() =>
            servico.AtualizarAsync(local.Id, Entrada("Outro"), Guid.NewGuid()));
    }
}

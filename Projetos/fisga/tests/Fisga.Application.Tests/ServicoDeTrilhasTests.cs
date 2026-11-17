using Fisga.Application.Abstractions;
using Fisga.Application.Trilhas;
using Fisga.Domain.Trilhas;

namespace Fisga.Application.Tests;

public sealed class ServicoDeTrilhasTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 8, 0, 0, TimeSpan.FromHours(-3));

    private static EntradaDeTrilha Entrada(string nome = "Corrico da laje") =>
        new(
            nome,
            "Borda do canal.",
            TipoDeTrilha.Corrico,
            [new Coordenada(-10.911, -37.071), new Coordenada(-10.921, -37.081)]);

    [Fact]
    public async Task Cria_e_lista_trilhas_do_usuario()
    {
        var repositorio = new TrilhaPrivadaRepositorioFake();
        var servico = new ServicoDeTrilhas(repositorio, new RelogioFixo(Agora));
        var usuarioId = Guid.NewGuid();

        var trilha = await servico.CriarAsync(Entrada(), usuarioId);
        var lista = await servico.ListarAsync(usuarioId);

        Assert.Single(lista);
        Assert.Equal(trilha.Id, lista[0].Id);
        Assert.Equal("Corrico", lista[0].Tipo);
        Assert.True(lista[0].DistanciaKm > 0);
    }

    [Fact]
    public async Task Nao_permite_editar_trilha_de_outra_conta()
    {
        var repositorio = new TrilhaPrivadaRepositorioFake();
        var servico = new ServicoDeTrilhas(repositorio, new RelogioFixo(Agora));
        var dono = Guid.NewGuid();
        var trilha = await servico.CriarAsync(Entrada(), dono);

        await Assert.ThrowsAsync<AcessoNegadoException>(() =>
            servico.AtualizarAsync(trilha.Id, Entrada("Outra"), Guid.NewGuid()));
    }

    [Fact]
    public async Task Exclui_trilha_do_dono()
    {
        var repositorio = new TrilhaPrivadaRepositorioFake();
        var servico = new ServicoDeTrilhas(repositorio, new RelogioFixo(Agora));
        var dono = Guid.NewGuid();
        var trilha = await servico.CriarAsync(Entrada(), dono);

        await servico.ExcluirAsync(trilha.Id, dono);

        Assert.Empty(await servico.ListarAsync(dono));
    }
}

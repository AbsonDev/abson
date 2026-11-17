using Fisga.Application.Abstractions;
using Fisga.Application.Pontos;
using Fisga.Domain.Common;
using Fisga.Domain.Pontos;

namespace Fisga.Application.Tests;

public sealed class ServicoDePontosTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 8, 0, 0, TimeSpan.FromHours(-3));

    private static EntradaDePonto Entrada(string nome = "Laje do Farol") =>
        new(nome, "Pedra a 8 m.", -10.911, -37.071, TipoDePonto.Estrutura);

    [Fact]
    public async Task Cria_e_lista_pontos_do_usuario()
    {
        var repositorio = new PontoPrivadoRepositorioFake();
        var servico = new ServicoDePontos(repositorio, new RelogioFixo(Agora));
        var usuarioId = Guid.NewGuid();

        var ponto = await servico.CriarAsync(Entrada(), usuarioId);
        var lista = await servico.ListarAsync(usuarioId);

        Assert.Single(lista);
        Assert.Equal(ponto.Id, lista[0].Id);
        Assert.Equal("Estrutura", lista[0].Tipo);
    }

    [Fact]
    public async Task Limita_duzentos_pontos_por_usuario()
    {
        var repositorio = new PontoPrivadoRepositorioFake();
        var servico = new ServicoDePontos(repositorio, new RelogioFixo(Agora));
        var usuarioId = Guid.NewGuid();

        for (var indice = 0; indice < ServicoDePontos.LimitePorUsuario; indice++)
        {
            await servico.CriarAsync(Entrada($"Ponto {indice}"), usuarioId);
        }

        var excecao = await Assert.ThrowsAsync<DomainException>(() =>
            servico.CriarAsync(Entrada("Excedente"), usuarioId));

        Assert.Equal("limite_de_pontos", excecao.Codigo);
    }

    [Fact]
    public async Task Nao_permite_editar_ponto_de_outra_conta()
    {
        var repositorio = new PontoPrivadoRepositorioFake();
        var servico = new ServicoDePontos(repositorio, new RelogioFixo(Agora));
        var dono = Guid.NewGuid();
        var ponto = await servico.CriarAsync(Entrada(), dono);

        await Assert.ThrowsAsync<AcessoNegadoException>(() =>
            servico.AtualizarAsync(ponto.Id, Entrada("Outro"), Guid.NewGuid()));
    }

    [Fact]
    public async Task Exclui_ponto_do_dono()
    {
        var repositorio = new PontoPrivadoRepositorioFake();
        var servico = new ServicoDePontos(repositorio, new RelogioFixo(Agora));
        var dono = Guid.NewGuid();
        var ponto = await servico.CriarAsync(Entrada(), dono);

        await servico.ExcluirAsync(ponto.Id, dono);

        Assert.Empty(await servico.ListarAsync(dono));
    }
}

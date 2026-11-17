using Fisga.Application.Catalogo;
using Fisga.Application.Pescarias;
using Fisga.Domain.Pescarias;

namespace Fisga.Application.Tests;

public sealed class ServicoDePescariasTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 16, 6, 30, 0, TimeSpan.FromHours(-3));
    private static readonly Guid UsuarioId = Guid.NewGuid();

    private static EntradaDePescaria Entrada(string local = "Represa do Lago Azul", params EntradaDeCaptura[] capturas) =>
        new()
        {
            Local = local,
            Pescador = "Abson",
            Cidade = "Campinas",
            Estado = "SP",
            Ambiente = AmbienteDePesca.Represa,
            Data = new DateOnly(2026, 11, 16),
            Periodo = PeriodoDoDia.Manha,
            Lua = FaseDaLua.Crescente,
            Clima = Clima.Sol,
            ClarezaDaAgua = ClarezaDaAgua.Turva,
            Observacoes = "Vento fraco.",
            Capturas = capturas.Length == 0
                ? [
                    new EntradaDeCaptura(null, "Tucunaré", 2.4m, 48m, "Popper", true, null, null),
                    new EntradaDeCaptura(null, "Tilápia", 0.8m, 28m, "Minhoca", false, null, null)
                ]
                : capturas
        };

    [Fact]
    public async Task Cria_pescaria_e_publica_evento()
    {
        var repositorio = new PescariaRepositorioFake();
        var eventos = new DispatcherFake();
        var servico = new ServicoDePescarias(repositorio, new RelogioFixo(Agora), eventos);

        var salva = await servico.CriarAsync(Entrada(), UsuarioId);

        Assert.Single(repositorio.Pescarias);
        Assert.Single(eventos.Publicados);
        Assert.Equal(2, repositorio.Pescarias[0].Capturas.Count);
        Assert.Equal(32, salva.ShareToken.Length);
        Assert.Equal(UsuarioId, repositorio.Pescarias[0].UsuarioId);
    }

    [Fact]
    public async Task Atualiza_preservando_captura_e_rejeita_outro_usuario()
    {
        var repositorio = new PescariaRepositorioFake();
        var servico = new ServicoDePescarias(repositorio, new RelogioFixo(Agora), new DispatcherFake());
        var salva = await servico.CriarAsync(Entrada(), UsuarioId);
        var captura = repositorio.Pescarias[0].Capturas.First();

        var atualizada = await servico.AtualizarAsync(
            salva.Id,
            UsuarioId,
            Entrada(capturas:
            [
                new EntradaDeCaptura(captura.Id, "Tucunaré", 3.2m, 52m, "Popper", true, null, "Cresceu")
            ]));

        Assert.Single(atualizada.Capturas);
        Assert.Equal(captura.Id, atualizada.Capturas[0].Id);
        Assert.Equal(3.2m, atualizada.Capturas[0].PesoKg);
        Assert.True(atualizada.PodeEditar);

        await Assert.ThrowsAsync<Abstractions.RecursoNaoEncontradoException>(() =>
            servico.AtualizarAsync(salva.Id, Guid.NewGuid(), Entrada()));
    }

    [Fact]
    public async Task Diario_calcula_estatisticas_e_resumos()
    {
        var repositorio = new PescariaRepositorioFake();
        var criador = new ServicoDePescarias(repositorio, new RelogioFixo(Agora), new DispatcherFake());
        await criador.CriarAsync(Entrada(), UsuarioId);
        await criador.CriarAsync(
            Entrada("Rio Tietê", new EntradaDeCaptura(null, "Pintado", 8.5m, 92m, "Lambari", false, null, null)),
            UsuarioId);

        var diario = new ServicoDoDiario(repositorio, new CatalogoDePesca());
        var resultado = await diario.ObterAsync(UsuarioId);

        Assert.Equal(2, resultado.TotalPescarias);
        Assert.Equal(3, resultado.TotalCapturas);
        Assert.Equal(3, resultado.EspeciesDistintas);
        Assert.Equal(1, resultado.TotalSoltos);
        Assert.Equal(8.5m, resultado.MaiorPesoKg);
        Assert.Equal(92m, resultado.MaiorComprimentoCm);
        Assert.Equal(2, resultado.Pescarias.Count);
        Assert.Equal("Pintado", resultado.EspecieTop);
    }

    [Fact]
    public async Task Diario_de_outro_usuario_nao_vaza_pescarias()
    {
        var repositorio = new PescariaRepositorioFake();
        var servico = new ServicoDePescarias(repositorio, new RelogioFixo(Agora), new DispatcherFake());
        await servico.CriarAsync(Entrada(), UsuarioId);

        var diario = new ServicoDoDiario(repositorio, new CatalogoDePesca());
        var outro = await diario.ObterAsync(Guid.NewGuid());

        Assert.Empty(outro.Pescarias);
        Assert.Equal(0, outro.TotalCapturas);
    }

    [Fact]
    public async Task Obter_publico_devolve_capturas_e_permissao()
    {
        var repositorio = new PescariaRepositorioFake();
        var servico = new ServicoDePescarias(repositorio, new RelogioFixo(Agora), new DispatcherFake());
        var salva = await servico.CriarAsync(Entrada(), UsuarioId);

        var publica = await servico.ObterPublicoAsync(salva.ShareToken);
        var doDono = await servico.ObterPublicoAsync(salva.ShareToken, UsuarioId);

        Assert.NotNull(publica);
        Assert.Equal(2, publica.Capturas.Count);
        Assert.False(publica.PodeEditar);
        Assert.All(publica.Capturas, captura => Assert.NotEqual(Guid.Empty, captura.Id));
        Assert.NotNull(doDono);
        Assert.True(doDono.PodeEditar);
    }

    [Fact]
    public async Task Exige_usuario_autenticado()
    {
        var servico = new ServicoDePescarias(
            new PescariaRepositorioFake(),
            new RelogioFixo(Agora),
            new DispatcherFake());

        await Assert.ThrowsAsync<Abstractions.NaoAutenticadoException>(() =>
            servico.CriarAsync(Entrada(), Guid.Empty));
    }
}

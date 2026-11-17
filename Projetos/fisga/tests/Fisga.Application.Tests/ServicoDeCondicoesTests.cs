using Fisga.Application.Condicoes;
using Fisga.Domain.Pescarias;

namespace Fisga.Application.Tests;

public sealed class ServicoDeCondicoesTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 12, 0, 0, TimeSpan.FromHours(-3));

    private static DadosDeCondicoes Dados(bool comMare = true)
    {
        var inicio = new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.FromHours(-3));
        var horas = new List<HoraDeCondicoes>();
        var dias = new List<DiaDeCondicoes>();

        for (var dia = 0; dia < 7; dia++)
        {
            for (var hora = 0; hora < 24; hora++)
            {
                var instante = inicio.AddDays(dia).AddHours(hora);
                var global = dia * 24 + hora;
                var nivel = comMare
                    ? 0.75 + 0.4 * Math.Sin(2 * Math.PI * (global - 10.33) / 12.42)
                    : (double?)null;

                horas.Add(new HoraDeCondicoes(
                    instante,
                    27,
                    29,
                    10,
                    1012 + global * 0.02,
                    12,
                    18,
                    90,
                    20,
                    nivel,
                    26,
                    0.8,
                    7));
            }

            dias.Add(new DiaDeCondicoes(
                new DateOnly(2026, 11, 17).AddDays(dia),
                inicio.AddDays(dia).AddHours(4).AddMinutes(53),
                inicio.AddDays(dia).AddHours(17).AddMinutes(33)));
        }

        var lua = new DadosDaLua(
            inicio.AddHours(11).AddMinutes(26),
            inicio.AddDays(1).AddMinutes(2),
            inicio.AddHours(17).AddMinutes(47),
            inicio.AddHours(5).AddMinutes(25),
            85.68);

        return new DadosDeCondicoes(horas, dias, lua, comMare, "Teste");
    }

    [Fact]
    public async Task Monta_condicoes_com_maré_solunar_e_sete_dias()
    {
        var servico = new ServicoDeCondicoes(
            new ProvedorDeCondicoesFake(Dados()),
            new RelogioFixo(Agora),
            new CondicoesOptions());

        var resultado = await servico.ObterAsync(
            -10.911,
            -37.071,
            "Aracaju",
            "Aracaju",
            "SE",
            AmbienteDePesca.Mar);

        Assert.True(resultado.TemMare);
        Assert.True(resultado.MareAproximada);
        Assert.NotNull(resultado.Mare);
        Assert.Equal(4, resultado.Mare!.Eventos.Count);
        Assert.Equal(24, resultado.Mare.Curva.Count);
        Assert.NotNull(resultado.Agora.ProximoEvento);
        Assert.NotEmpty(resultado.Solunar.Periodos);
        Assert.Equal(7, resultado.Dias.Count);
        Assert.NotNull(resultado.MelhorJanela);
        Assert.Equal("America/Sao_Paulo", resultado.Fuso);
    }

    [Fact]
    public async Task Local_sem_maré_nao_inventa_tábua()
    {
        var servico = new ServicoDeCondicoes(
            new ProvedorDeCondicoesFake(Dados(comMare: false)),
            new RelogioFixo(Agora),
            new CondicoesOptions());

        var resultado = await servico.ObterAsync(
            -22.9,
            -47.0,
            "Represa",
            "Campinas",
            "SP",
            AmbienteDePesca.Represa);

        Assert.False(resultado.TemMare);
        Assert.Null(resultado.Mare);
        Assert.NotNull(resultado.Solunar);
        Assert.NotEmpty(resultado.Dias);
    }

    [Fact]
    public async Task Rejeita_coordenadas_invalidas()
    {
        var servico = new ServicoDeCondicoes(
            new ProvedorDeCondicoesFake(Dados()),
            new RelogioFixo(Agora),
            new CondicoesOptions());

        await Assert.ThrowsAsync<Domain.Common.DomainException>(() =>
            servico.ObterAsync(100, -37, "X", null, null, AmbienteDePesca.Mar));
    }

    [Fact]
    public void Geocodificador_de_estado_mapeia_nome_para_uf()
    {
        Assert.Equal("SE", EstadosBrasileiros.ParaUf("Sergipe"));
        Assert.Equal("SP", EstadosBrasileiros.ParaUf("São Paulo"));
        Assert.Null(EstadosBrasileiros.ParaUf("Lugar nenhum"));
    }
}

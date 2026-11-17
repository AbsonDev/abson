using Fisga.Application.Condicoes;
using Fisga.Domain.Pescarias;

namespace Fisga.Application.Tests;

public sealed class AstronomiaTests
{
    [Fact]
    public void Angulo_de_fase_do_MET_vira_lua_crescente_com_quase_metade_iluminada()
    {
        var fase = Astronomia.FasePorAngulo(85.68);

        Assert.Equal(FaseDaLua.Crescente, fase.Fase);
        Assert.InRange(fase.Iluminacao, 43, 49);
        Assert.InRange(fase.IdadeDias, 6.5, 7.5);
    }

    [Fact]
    public void Transito_lunar_gera_periodo_maior_de_duas_horas()
    {
        var eventos = new EventosDaLua(
            new DateTimeOffset(2026, 11, 17, 17, 47, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 11, 17, 5, 25, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 11, 17, 11, 26, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 11, 18, 0, 2, 0, TimeSpan.FromHours(-3)));

        var nascer = new DateTimeOffset(2026, 11, 17, 4, 53, 0, TimeSpan.FromHours(-3));
        var por = new DateTimeOffset(2026, 11, 17, 17, 33, 0, TimeSpan.FromHours(-3));

        var janelas = Astronomia.Janelas(eventos, nascer, por);

        Assert.Contains(janelas, janela =>
            janela.Tipo == "Maior"
            && janela.Inicio == eventos.Transito!.Value.AddHours(-1)
            && janela.Fim == eventos.Transito.Value.AddHours(1));
        Assert.Contains(janelas, janela => janela.Motivo.Contains("entardecer"));
    }

    [Fact]
    public void Janelas_maiores_tem_duas_horas_e_menores_uma_hora()
    {
        var eventos = new EventosDaLua(
            new DateTimeOffset(2026, 11, 17, 17, 47, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 11, 17, 5, 25, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 11, 17, 11, 26, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 11, 18, 0, 2, 0, TimeSpan.FromHours(-3)));
        var nascer = new DateTimeOffset(2026, 11, 17, 4, 53, 0, TimeSpan.FromHours(-3));
        var por = new DateTimeOffset(2026, 11, 17, 17, 33, 0, TimeSpan.FromHours(-3));

        var janelas = Astronomia.Janelas(eventos, nascer, por);

        Assert.Contains(janelas, janela => janela.Tipo == "Maior" && janela.Fim - janela.Inicio == TimeSpan.FromHours(2));
        Assert.Contains(janelas, janela => janela.Tipo == "Menor" && janela.Fim - janela.Inicio == TimeSpan.FromHours(1));
    }
}

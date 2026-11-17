using Fisga.Domain.Pescarias;

namespace Fisga.Application.Condicoes;

public sealed record FaseDaLuaInfo(
    FaseDaLua Fase,
    string Nome,
    double Iluminacao,
    double IdadeDias);

public sealed record EventosDaLua(
    DateTimeOffset? Transito,
    DateTimeOffset? TransitoOposto,
    DateTimeOffset? Nascer,
    DateTimeOffset? Por);

public sealed record JanelaSolunar(
    string Tipo,
    DateTimeOffset Inicio,
    DateTimeOffset Fim,
    string Motivo,
    string Atividade);

/// <summary>
/// Astronomia de bolso: fase da lua, trânsitos e nascimento/pôr com precisão
/// suficiente para janelas de pesca (não para navegação).
/// </summary>
public static class Astronomia
{
    private const double Radiano = Math.PI / 180.0;
    private const double MesSinodico = 29.530588853;
    private const double LuaNovaReferencia = 2451550.26; // 2000-01-06 18:14 UTC

    public static FaseDaLuaInfo FasePorAngulo(double angulo)
    {
        var normalizado = Mod(angulo, 360);
        var idade = normalizado / 360 * MesSinodico;
        var iluminacao = (1 - Math.Cos(normalizado * Radiano)) / 2 * 100;
        var (fase, nome) = Nomear(idade);

        return new FaseDaLuaInfo(fase, nome, iluminacao, idade);
    }

    public static FaseDaLuaInfo Fase(DateTimeOffset quando)
    {
        var julian = JulianDay(quando.UtcDateTime);
        var idade = Mod(julian - LuaNovaReferencia, MesSinodico);
        var iluminacao = (1 - Math.Cos(2 * Math.PI * idade / MesSinodico)) / 2;

        var (fase, nome) = Nomear(idade);

        return new FaseDaLuaInfo(fase, nome, iluminacao * 100, idade);
    }

    private static (FaseDaLua Fase, string Nome) Nomear(double idade) => idade switch
    {
        < 1.845 => (FaseDaLua.Nova, "Lua nova"),
        < 5.536 => (FaseDaLua.Crescente, "Lua crescente"),
        < 9.228 => (FaseDaLua.Crescente, "Quarto crescente"),
        < 12.919 => (FaseDaLua.Crescente, "Crescente gibosa"),
        < 16.610 => (FaseDaLua.Cheia, "Lua cheia"),
        < 20.302 => (FaseDaLua.Minguante, "Minguante gibosa"),
        < 23.993 => (FaseDaLua.Minguante, "Quarto minguante"),
        < 27.684 => (FaseDaLua.Minguante, "Lua minguante"),
        _ => (FaseDaLua.Nova, "Lua nova")
    };

    public static EventosDaLua Eventos(
        DateOnly dia,
        double latitude,
        double longitude,
        TimeZoneInfo fuso)
    {
        var inicioLocal = dia.ToDateTime(TimeOnly.MinValue);
        var inicioUtc = TimeZoneInfo.ConvertTimeToUtc(inicioLocal, fuso);
        var fimUtc = inicioUtc.AddDays(1);

        DateTimeOffset? transito = null;
        DateTimeOffset? transitoOposto = null;
        DateTimeOffset? nascer = null;
        DateTimeOffset? por = null;

        var passo = TimeSpan.FromMinutes(10);
        var anterior = inicioUtc.AddHours(-2);
        var limite = fimUtc.AddHours(2);

        double? hAnterior = null;
        double? haAnterior = null;
        double? altAnterior = null;
        var houveTransito = false;
        var houveOposto = false;
        var houveNascer = false;
        var houvePor = false;

        for (var atual = anterior; atual <= limite; atual += passo)
        {
            var d = JulianDay(atual) - 2451545.0;
            var (ra, dec) = PosicaoLua(d);
            var lst = Mod(Gmst(d) + longitude, 360);
            var h = AnguloAssinado(lst - ra);
            var ha = AnguloAssinado(lst - ra - 180);

            if (hAnterior is not null && !houveTransito && hAnterior < 0 && h >= 0 && h - hAnterior < 180)
            {
                var fracao = -hAnterior.Value / (h - hAnterior.Value);
                var instante = anterior + passo * fracao;
                transito = ParaLocal(instante, fuso);
                houveTransito = true;
            }

            if (haAnterior is not null && !houveOposto && haAnterior < 0 && ha >= 0 && ha - haAnterior < 180)
            {
                var fracao = -haAnterior.Value / (ha - haAnterior.Value);
                var instante = anterior + passo * fracao;
                transitoOposto = ParaLocal(instante, fuso);
                houveOposto = true;
            }

            var altitude = Altitude(dec, latitude, h);
            if (altAnterior is not null)
            {
                if (!houveNascer && altAnterior < 0.125 && altitude >= 0.125)
                {
                    var fracao = (0.125 - altAnterior.Value) / (altitude - altAnterior.Value);
                    nascer = ParaLocal(anterior + passo * fracao, fuso);
                    houveNascer = true;
                }

                if (!houvePor && altAnterior >= 0.125 && altitude < 0.125)
                {
                    var fracao = (altAnterior.Value - 0.125) / (altAnterior.Value - altitude);
                    por = ParaLocal(anterior + passo * fracao, fuso);
                    houvePor = true;
                }
            }

            hAnterior = h;
            haAnterior = ha;
            altAnterior = altitude;
        }

        return new EventosDaLua(transito, transitoOposto, nascer, por);
    }

    public static IReadOnlyList<JanelaSolunar> Janelas(
        EventosDaLua eventos,
        DateTimeOffset nascerDoSol,
        DateTimeOffset porDoSol)
    {
        var janelas = new List<JanelaSolunar>();

        AdicionarMaior(janelas, eventos.Transito, "Lua sobre a sua cabeça", nascerDoSol, porDoSol);
        AdicionarMaior(janelas, eventos.TransitoOposto, "Lua sob os seus pés", nascerDoSol, porDoSol);
        AdicionarMenor(janelas, eventos.Nascer, "Nascer da lua", nascerDoSol, porDoSol);
        AdicionarMenor(janelas, eventos.Por, "Pôr da lua", nascerDoSol, porDoSol);

        return janelas
            .DistinctBy(janela => (janela.Tipo, janela.Inicio))
            .OrderBy(janela => janela.Inicio)
            .ToList();
    }

    private static void AdicionarMaior(
        List<JanelaSolunar> janelas,
        DateTimeOffset? centro,
        string motivo,
        DateTimeOffset nascerDoSol,
        DateTimeOffset porDoSol)
    {
        if (centro is null)
        {
            return;
        }

        var inicio = centro.Value.AddHours(-1);
        var fim = centro.Value.AddHours(1);
        var coincide = CoincideComSol(inicio, fim, nascerDoSol, porDoSol);
        var motivoFinal = coincide is null ? motivo : $"{motivo} · coincide com {coincide}";

        janelas.Add(new JanelaSolunar(
            "Maior",
            inicio,
            fim,
            motivoFinal,
            coincide is null ? "Muito alta" : "Muito alta"));
    }

    private static void AdicionarMenor(
        List<JanelaSolunar> janelas,
        DateTimeOffset? centro,
        string motivo,
        DateTimeOffset nascerDoSol,
        DateTimeOffset porDoSol)
    {
        if (centro is null)
        {
            return;
        }

        var inicio = centro.Value.AddMinutes(-30);
        var fim = centro.Value.AddMinutes(30);
        var coincide = CoincideComSol(inicio, fim, nascerDoSol, porDoSol);
        var motivoFinal = coincide is null ? motivo : $"{motivo} · coincide com {coincide}";

        janelas.Add(new JanelaSolunar(
            "Menor",
            inicio,
            fim,
            motivoFinal,
            coincide is null ? "Alta" : "Muito alta"));
    }

    private static string? CoincideComSol(
        DateTimeOffset inicio,
        DateTimeOffset fim,
        DateTimeOffset nascerDoSol,
        DateTimeOffset porDoSol)
    {
        var margem = TimeSpan.FromMinutes(45);
        if (inicio <= nascerDoSol.Add(margem) && fim >= nascerDoSol.Subtract(margem))
        {
            return "o amanhecer";
        }

        if (inicio <= porDoSol.Add(margem) && fim >= porDoSol.Subtract(margem))
        {
            return "o entardecer";
        }

        return null;
    }

    private static (double Ra, double Dec) PosicaoLua(double d)
    {
        var l = Mod(218.316 + 13.176396 * d, 360);
        var m = Mod(134.963 + 13.064993 * d, 360);
        var f = Mod(93.272 + 13.229350 * d, 360);

        var lambda = Mod(l + 6.289 * Math.Sin(m * Radiano), 360) * Radiano;
        var beta = 5.128 * Math.Sin(f * Radiano) * Radiano;
        var eps = (23.4397 - 0.00000036 * d) * Radiano;

        var ra = Math.Atan2(
            Math.Sin(lambda) * Math.Cos(eps) - Math.Tan(beta) * Math.Sin(eps),
            Math.Cos(lambda));
        var dec = Math.Asin(
            Math.Sin(beta) * Math.Cos(eps)
            + Math.Cos(beta) * Math.Sin(eps) * Math.Sin(lambda));

        return (Mod(ra / Radiano, 360), dec / Radiano);
    }

    private static double Altitude(double declinacao, double latitude, double anguloHorario)
    {
        var dec = declinacao * Radiano;
        var lat = latitude * Radiano;
        var h = anguloHorario * Radiano;

        var seno = Math.Sin(dec) * Math.Sin(lat)
                   + Math.Cos(dec) * Math.Cos(lat) * Math.Cos(h);

        return Math.Asin(Math.Clamp(seno, -1, 1)) / Radiano;
    }

    private static double Gmst(double d) => Mod(280.46061837 + 360.98564736629 * d, 360);

    private static double JulianDay(DateTime utc) => utc.ToOADate() + 2415018.5;

    private static double AnguloAssinado(double graus) => Mod(graus + 180, 360) - 180;

    private static double Mod(double valor, double divisor)
    {
        var resultado = valor % divisor;
        return resultado < 0 ? resultado + divisor : resultado;
    }

    private static DateTimeOffset ParaLocal(DateTime utc, TimeZoneInfo fuso)
    {
        var offset = fuso.GetUtcOffset(utc);
        return new DateTimeOffset(utc, TimeSpan.Zero).ToOffset(offset);
    }
}

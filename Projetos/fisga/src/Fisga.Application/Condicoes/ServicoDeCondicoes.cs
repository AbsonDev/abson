using Fisga.Application.Abstractions;
using Fisga.Domain.Common;
using Fisga.Domain.Pescarias;

namespace Fisga.Application.Condicoes;

public sealed class ServicoDeCondicoes(
    IProvedorDeCondicoes provedor,
    IRelogio relogio,
    CondicoesOptions opcoes)
{
    private static readonly string[] Direcoes = ["N", "NE", "L", "SE", "S", "SO", "O", "NO"];

    public async Task<CondicoesDto> ObterAsync(
        double latitude,
        double longitude,
        string? nome,
        string? cidade,
        string? estado,
        AmbienteDePesca ambiente,
        CancellationToken ct = default)
    {
        if (double.IsNaN(latitude) || latitude is < -90 or > 90)
        {
            throw new DomainException("latitude_invalida", "A latitude deve estar entre -90 e 90.");
        }

        if (double.IsNaN(longitude) || longitude is < -180 or > 180)
        {
            throw new DomainException("longitude_invalida", "A longitude deve estar entre -180 e 180.");
        }

        var fuso = Fuso();
        var agora = relogio.Agora.ToOffset(fuso.GetUtcOffset(relogio.Agora));
        var hoje = DateOnly.FromDateTime(agora.DateTime);

        var dados = await provedor.ObterAsync(latitude, longitude, hoje, ct);
        if (dados is null || dados.Horas.Count == 0)
        {
            throw new RecursoNaoEncontradoException(
                "condicoes_indisponiveis",
                "Não conseguimos obter as condições deste local agora. Tente novamente.");
        }

        var temMare = dados.TemMare && dados.Horas.Any(hora => hora.NivelDoMarM is not null);
        var horaAtual = HoraMaisProxima(dados.Horas, agora);
        var (nascerHoje, porHoje) = HorarioDoDia(dados, hoje, fuso);

        var solunar = ConstruirSolunar(
            latitude,
            longitude,
            hoje,
            agora,
            fuso,
            nascerHoje,
            porHoje,
            dados.Lua);

        var mare = temMare ? ConstruirMare(dados.Horas, agora, hoje) : null;
        var janelaHoje = MelhorDoDia(
            hoje,
            dados.Horas,
            solunar.Janelas,
            nascerHoje,
            porHoje,
            agora,
            temMare);

        var dias = ConstruirDias(dados, latitude, longitude, agora, fuso, temMare, hoje);

        var tempo = new TempoDto(
            horaAtual.VentoKmh,
            horaAtual.RajadaKmh,
            horaAtual.VentoDirecaoGraus,
            Direcao(horaAtual.VentoDirecaoGraus),
            horaAtual.ChuvaProbabilidade,
            horaAtual.TemperaturaC,
            horaAtual.SensacaoC,
            horaAtual.PressaoHpa,
            TendenciaDePressao(dados.Horas, horaAtual),
            AlertaDePressao(dados.Horas, horaAtual),
            horaAtual.CorrenteKmh,
            horaAtual.CorrenteDirecaoGraus,
            horaAtual.CorrenteDirecaoGraus is null
                ? null
                : Direcao(horaAtual.CorrenteDirecaoGraus.Value),
            horaAtual.TemperaturaDaAguaC,
            horaAtual.OndaAlturaM,
            horaAtual.OndaPeriodoS);

        var linhaDoTempo = ConstruirLinhaDoTempo(
            dados.Horas,
            hoje,
            solunar.Janelas,
            nascerHoje,
            porHoje,
            temMare);

        return new CondicoesDto(
            new LocalDeCondicaoDto(
                string.IsNullOrWhiteSpace(nome) ? "Local sem nome" : nome.Trim(),
                string.IsNullOrWhiteSpace(cidade) ? null : cidade.Trim(),
                string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant(),
                latitude,
                longitude,
                ambiente.ToString()),
            agora,
            opcoes.Fuso,
            dados.Fonte,
            temMare,
            temMare,
            temMare ? opcoes.AvisoMare : "Este local não tem maré. Mostramos vento, chuva, lua e solunar.",
            ConstruirAgora(mare, tempo),
            janelaHoje?.Janela,
            dias,
            mare,
            solunar.Dto,
            tempo,
            linhaDoTempo);
    }

    private static (SolunarDto Dto, IReadOnlyList<JanelaSolunar> Janelas) ConstruirSolunar(
        double latitude,
        double longitude,
        DateOnly dia,
        DateTimeOffset agora,
        TimeZoneInfo fuso,
        DateTimeOffset nascerDoSol,
        DateTimeOffset porDoSol,
        DadosDaLua? lua)
    {
        var fase = lua is not null
            ? Astronomia.FasePorAngulo(lua.FaseAngulo)
            : Astronomia.Fase(agora);
        var eventos = lua is not null
            ? new EventosDaLua(lua.Transito, lua.TransitoOposto, lua.Nascer, lua.Por)
            : Astronomia.Eventos(dia, latitude, longitude, fuso);
        var janelas = Astronomia.Janelas(eventos, nascerDoSol, porDoSol)
            .Where(janela => janela.Fim > janela.Inicio)
            .ToList();

        var dto = new SolunarDto(
            fase.Fase.ToString(),
            fase.Nome,
            Math.Round(fase.Iluminacao, 0),
            Math.Round(fase.IdadeDias, 1),
            janelas
                .Select(janela => new PeriodoSolunarDto(
                    janela.Tipo,
                    janela.Inicio,
                    janela.Fim,
                    janela.Motivo,
                    janela.Atividade))
                .ToList());

        return (dto, janelas);
    }

    private static MareDto ConstruirMare(
        IReadOnlyList<HoraDeCondicoes> horas,
        DateTimeOffset agora,
        DateOnly hoje)
    {
        var eventosHoje = EventosDeMare(horas, hoje);
        var eventosAmanha = EventosDeMare(horas, hoje.AddDays(1));
        var proximo = eventosHoje
            .Concat(eventosAmanha)
            .FirstOrDefault(evento => evento.Hora > agora);
        var estado = proximo is null
            ? "estável"
            : proximo.Tipo == "Cheia" ? "subindo" : "descendo";

        var curva = horas
            .Where(hora => DateOnly.FromDateTime(hora.Hora.DateTime) == hoje && hora.NivelDoMarM is not null)
            .Select(hora => new PontoDaMareDto(hora.Hora, Math.Round(hora.NivelDoMarM!.Value, 2)))
            .ToList();

        return new MareDto(
            estado,
            "Nível médio do mar (modelo)",
            proximo is null ? null : ParaEventoDto(proximo, agora),
            eventosHoje.Select(evento => ParaEventoDto(evento, agora)).ToList(),
            curva);
    }

    private static EventoDeMareDto ParaEventoDto(EventoDeMare evento, DateTimeOffset agora) =>
        new(
            evento.Tipo,
            ArredondarMinuto(evento.Hora),
            Math.Round(evento.Altura, 2),
            Math.Round((evento.Hora - agora).TotalHours, 1));

    private static DateTimeOffset ArredondarMinuto(DateTimeOffset valor)
    {
        var ticks = valor.Ticks;
        var resto = ticks % TimeSpan.TicksPerMinute;
        ticks = resto >= TimeSpan.TicksPerMinute / 2
            ? ticks + TimeSpan.TicksPerMinute - resto
            : ticks - resto;

        return new DateTimeOffset(ticks, valor.Offset);
    }

    private static IReadOnlyList<HoraDoDiaDto> ConstruirLinhaDoTempo(
        IReadOnlyList<HoraDeCondicoes> horas,
        DateOnly dia,
        IReadOnlyList<JanelaSolunar> janelas,
        DateTimeOffset nascer,
        DateTimeOffset por,
        bool temMare)
    {
        var doDia = horas
            .Where(hora => DateOnly.FromDateTime(hora.Hora.DateTime) == dia)
            .OrderBy(hora => hora.Hora)
            .ToList();

        return doDia
            .Select(hora =>
            {
                var nota = Math.Clamp(
                    (int)Math.Round(Pontuar(hora, horas, janelas, nascer, por, temMare) / 20.0),
                    1,
                    5);
                var solunar = janelas.Any(janela =>
                    hora.Hora < janela.Fim && hora.Hora.AddHours(1) > janela.Inicio);

                return new HoraDoDiaDto(
                    hora.Hora,
                    hora.NivelDoMarM is null ? null : Math.Round(hora.NivelDoMarM.Value, 2),
                    hora.VentoKmh,
                    hora.ChuvaProbabilidade,
                    hora.PressaoHpa,
                    nota,
                    solunar);
            })
            .ToList();
    }

    private static IReadOnlyList<DiaDto> ConstruirDias(
        DadosDeCondicoes dados,
        double latitude,
        double longitude,
        DateTimeOffset agora,
        TimeZoneInfo fuso,
        bool temMare,
        DateOnly inicio)
    {
        var dias = new List<DiaDto>();

        for (var deslocamento = 0; deslocamento < 7; deslocamento++)
        {
            var dia = inicio.AddDays(deslocamento);
            var (nascer, por) = HorarioDoDia(dados, dia, fuso);
            var eventos = EventosDoDia(dados.Lua, dia, inicio, latitude, longitude, fuso);
            var janelas = Astronomia.Janelas(eventos, nascer, por);
            var resultado = MelhorDoDia(
                dia,
                dados.Horas,
                janelas,
                nascer,
                por,
                agora,
                temMare);

            if (resultado is null)
            {
                if (dia == inicio)
                {
                    dias.Add(new DiaDto(
                        dia,
                        1,
                        "Fraca",
                        null,
                        null,
                        "Dia quase encerrado. Confira amanhã.",
                        nascer,
                        por));
                }

                continue;
            }

            dias.Add(new DiaDto(
                dia,
                resultado.Janela?.Nota ?? 1,
                resultado.Janela?.Forca ?? "Fraca",
                resultado.Janela?.Inicio,
                resultado.Janela?.Fim,
                resultado.Resumo,
                nascer,
                por));
        }

        return dias;
    }

    private static ResultadoDoDia? MelhorDoDia(
        DateOnly dia,
        IReadOnlyList<HoraDeCondicoes> horas,
        IReadOnlyList<JanelaSolunar> janelas,
        DateTimeOffset nascer,
        DateTimeOffset por,
        DateTimeOffset agora,
        bool temMare)
    {
        var doDia = horas
            .Where(hora => DateOnly.FromDateTime(hora.Hora.DateTime) == dia)
            .OrderBy(hora => hora.Hora)
            .ToList();

        if (doDia.Count < 4)
        {
            return null;
        }

        var limite = DateOnly.FromDateTime(agora.DateTime) == dia ? agora.AddMinutes(-30) : (DateTimeOffset?)null;

        var avaliadas = doDia
            .Select(hora => (Hora: hora, Nota: Pontuar(hora, horas, janelas, nascer, por, temMare)))
            .ToList();

        (DateTimeOffset Inicio, DateTimeOffset Fim, double Nota, HoraDeCondicoes Primeira, HoraDeCondicoes Segunda)? melhor = null;

        for (var indice = 0; indice < avaliadas.Count - 1; indice++)
        {
            var primeira = avaliadas[indice];
            var segunda = avaliadas[indice + 1];
            var inicio = primeira.Hora.Hora;
            var fim = inicio.AddHours(2);

            if (limite is not null && fim <= limite.Value)
            {
                continue;
            }

            var nota = (primeira.Nota + segunda.Nota) / 2;
            if (melhor is null || nota > melhor.Value.Nota)
            {
                melhor = (inicio, fim, nota, primeira.Hora, segunda.Hora);
            }
        }

        if (melhor is null)
        {
            return null;
        }

        var notaFinal = melhor.Value.Nota switch
        {
            >= 80 => 5,
            >= 65 => 4,
            >= 50 => 3,
            >= 35 => 2,
            _ => 1
        };
        var motivos = Motivos(melhor.Value.Primeira, melhor.Value.Segunda, janelas, nascer, por);
        var forca = notaFinal >= 4 ? "Forte" : notaFinal == 3 ? "Boa" : "Fraca";

        return new ResultadoDoDia(
            new JanelaDto(
                melhor.Value.Inicio,
                melhor.Value.Fim,
                notaFinal,
                forca,
                motivos),
            Resumo(melhor.Value.Inicio, motivos));
    }

    private static double Pontuar(
        HoraDeCondicoes hora,
        IReadOnlyList<HoraDeCondicoes> todas,
        IReadOnlyList<JanelaSolunar> janelas,
        DateTimeOffset nascer,
        DateTimeOffset por,
        bool temMare)
    {
        var nota = 30.0;
        var janela = janelas.FirstOrDefault(item =>
            hora.Hora < item.Fim && hora.Hora.AddHours(1) > item.Inicio);

        if (janela is not null)
        {
            nota += janela.Tipo == "Maior" ? 22 : 10;
        }

        if (temMare && hora.NivelDoMarM is not null)
        {
            var indice = IndiceDe(todas, hora);
            var anterior = indice > 0 ? todas[indice - 1].NivelDoMarM : null;
            var proximo = indice < todas.Count - 1 ? todas[indice + 1].NivelDoMarM : null;

            if (anterior is not null && proximo is not null)
            {
                var movimento = Math.Abs(proximo.Value - anterior.Value);
                nota += movimento >= 0.08 ? 12 : movimento >= 0.03 ? 6 : -6;
            }
        }

        nota += hora.VentoKmh switch
        {
            <= 8 => 10,
            <= 15 => 6,
            <= 22 => 2,
            <= 30 => -4,
            _ => -10
        };

        nota += hora.ChuvaProbabilidade switch
        {
            <= 15 => 8,
            <= 35 => 4,
            <= 60 => -4,
            _ => -10
        };

        if (Perto(hora.Hora, nascer) || Perto(hora.Hora, por))
        {
            nota += 6;
        }

        var indicePressao = IndiceDe(todas, hora);
        if (indicePressao >= 3)
        {
            var variacao = hora.PressaoHpa - todas[indicePressao - 3].PressaoHpa;
            nota += variacao > 0.7 ? 4 : variacao < -0.7 ? -4 : 1;
        }

        return Math.Clamp(nota, 0, 100);
    }

    private static IReadOnlyList<string> Motivos(
        HoraDeCondicoes primeira,
        HoraDeCondicoes segunda,
        IReadOnlyList<JanelaSolunar> janelas,
        DateTimeOffset nascer,
        DateTimeOffset por)
    {
        var motivos = new List<string>();
        var janela = janelas.FirstOrDefault(item =>
            primeira.Hora < item.Fim && segunda.Hora.AddHours(1) > item.Inicio);

        if (janela is not null)
        {
            motivos.Add(janela.Tipo == "Maior" ? "Período solunar maior" : "Período solunar");
        }

        if (primeira.NivelDoMarM is not null && segunda.NivelDoMarM is not null)
        {
            var variacao = segunda.NivelDoMarM.Value - primeira.NivelDoMarM.Value;
            motivos.Add(variacao > 0.03 ? "Maré enchendo" : variacao < -0.03 ? "Maré vazando" : "Maré parada");
        }

        if (primeira.VentoKmh <= 15)
        {
            motivos.Add("Vento fraco");
        }
        else if (primeira.VentoKmh <= 22)
        {
            motivos.Add("Vento moderado");
        }

        if (primeira.ChuvaProbabilidade <= 30)
        {
            motivos.Add("Chuva improvável");
        }

        if (Perto(primeira.Hora, nascer))
        {
            motivos.Add("Amanhecer");
        }
        else if (Perto(primeira.Hora, por))
        {
            motivos.Add("Entardecer");
        }

        return motivos
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToList();
    }

    private static string Resumo(DateTimeOffset inicio, IReadOnlyList<string> motivos)
    {
        var principal = motivos.FirstOrDefault();
        return principal is null
            ? $"Melhor às {inicio:HH:mm}."
            : $"{principal} · melhor às {inicio:HH:mm}.";
    }

    private static AgoraDto ConstruirAgora(MareDto? mare, TempoDto tempo)
    {
        var vento = tempo.VentoKmh <= 10 ? "vento fraco" : tempo.VentoKmh <= 22 ? "vento moderado" : "vento forte";
        var chuva = tempo.ChuvaProbabilidade <= 20
            ? "chuva improvável"
            : $"{Math.Round(tempo.ChuvaProbabilidade)}% de chuva";

        if (mare?.ProximoEvento is null)
        {
            return new AgoraDto(
                $"Sem maré neste local. {Capitalizar(vento)} e {chuva}.",
                null,
                null,
                null);
        }

        var evento = mare.ProximoEvento;
        var tipo = evento.Tipo == "Cheia" ? "Preamar" : "Baixa-mar";
        var em = evento.EmHoras is null or < 0
            ? "agora"
            : $"em {FormatarHoras(evento.EmHoras.Value)}";
        var resumo = $"Maré {mare.Estado} até {evento.Hora:HH:mm}. {Capitalizar(vento)} e {chuva}.";

        return new AgoraDto(
            resumo,
            mare.Estado,
            $"{tipo} às {evento.Hora:HH:mm}",
            em);
    }

    private static IReadOnlyList<EventoDeMare> EventosDeMare(
        IReadOnlyList<HoraDeCondicoes> horas,
        DateOnly dia)
    {
        var comNivel = horas
            .Where(hora => hora.NivelDoMarM is not null)
            .OrderBy(hora => hora.Hora)
            .ToList();

        var eventos = new List<EventoDeMare>();

        for (var indice = 1; indice < comNivel.Count - 1; indice++)
        {
            var anterior = comNivel[indice - 1].NivelDoMarM!.Value;
            var atual = comNivel[indice].NivelDoMarM!.Value;
            var proximo = comNivel[indice + 1].NivelDoMarM!.Value;

            if ((atual - anterior) * (proximo - atual) >= 0)
            {
                continue;
            }

            var denominador = anterior - 2 * atual + proximo;
            if (Math.Abs(denominador) < 0.0001)
            {
                continue;
            }

            var delta = 0.5 * (anterior - proximo) / denominador;
            var instante = comNivel[indice].Hora.AddHours(delta);
            if (DateOnly.FromDateTime(instante.DateTime) != dia)
            {
                continue;
            }

            var altura = atual - 0.25 * (anterior - proximo) * delta;
            eventos.Add(new EventoDeMare(
                atual > anterior ? "Cheia" : "Baixa",
                instante,
                altura));
        }

        return eventos
            .OrderBy(evento => evento.Hora)
            .Take(4)
            .ToList();
    }

    private static EventosDaLua EventosDoDia(
        DadosDaLua? lua,
        DateOnly dia,
        DateOnly inicio,
        double latitude,
        double longitude,
        TimeZoneInfo fuso)
    {
        if (lua is null)
        {
            return Astronomia.Eventos(dia, latitude, longitude, fuso);
        }

        var deslocamento = TimeSpan.FromMinutes((24 * 60 + 50) * (dia.DayNumber - inicio.DayNumber));

        return new EventosDaLua(
            Somar(lua.Transito, deslocamento),
            Somar(lua.TransitoOposto, deslocamento),
            Somar(lua.Nascer, deslocamento),
            Somar(lua.Por, deslocamento));
    }

    private static DateTimeOffset? Somar(DateTimeOffset? valor, TimeSpan deslocamento) =>
        valor?.Add(deslocamento);

    private static (DateTimeOffset Nascer, DateTimeOffset Por) HorarioDoDia(
        DadosDeCondicoes dados,
        DateOnly dia,
        TimeZoneInfo fuso)
    {
        var diaBruto = dados.Dias.FirstOrDefault(item => item.Data == dia);
        if (diaBruto is not null)
        {
            return (diaBruto.NascerDoSol, diaBruto.PorDoSol);
        }

        var nascerLocal = dia.ToDateTime(new TimeOnly(6, 0));
        var porLocal = dia.ToDateTime(new TimeOnly(18, 0));
        var nascerUtc = TimeZoneInfo.ConvertTimeToUtc(nascerLocal, fuso);
        var porUtc = TimeZoneInfo.ConvertTimeToUtc(porLocal, fuso);

        return (
            new DateTimeOffset(nascerUtc, TimeSpan.Zero).ToOffset(fuso.GetUtcOffset(nascerUtc)),
            new DateTimeOffset(porUtc, TimeSpan.Zero).ToOffset(fuso.GetUtcOffset(porUtc)));
    }

    private static HoraDeCondicoes HoraMaisProxima(
        IReadOnlyList<HoraDeCondicoes> horas,
        DateTimeOffset agora) =>
        horas
            .OrderBy(hora => Math.Abs((hora.Hora - agora).TotalMinutes))
            .First();

    private static string? AlertaDePressao(
        IReadOnlyList<HoraDeCondicoes> horas,
        HoraDeCondicoes atual)
    {
        var indice = IndiceDe(horas, atual);
        if (indice < 3)
        {
            return null;
        }

        var variacao = atual.PressaoHpa - horas[indice - 3].PressaoHpa;
        return variacao switch
        {
            <= -2 => "Queda brusca de pressão: os peixes tendem a se alimentar antes da mudança.",
            >= 2 => "Subida brusca de pressão: a atividade pode cair.",
            _ => null
        };
    }

    private static string TendenciaDePressao(
        IReadOnlyList<HoraDeCondicoes> horas,
        HoraDeCondicoes atual)
    {
        var indice = IndiceDe(horas, atual);
        if (indice < 3)
        {
            return "estável";
        }

        var variacao = atual.PressaoHpa - horas[indice - 3].PressaoHpa;
        return variacao > 0.7 ? "subindo" : variacao < -0.7 ? "caindo" : "estável";
    }

    private static int IndiceDe(
        IReadOnlyList<HoraDeCondicoes> horas,
        HoraDeCondicoes alvo)
    {
        for (var indice = 0; indice < horas.Count; indice++)
        {
            if (horas[indice].Hora == alvo.Hora)
            {
                return indice;
            }
        }

        return -1;
    }

    private static bool Perto(DateTimeOffset hora, DateTimeOffset referencia) =>
        Math.Abs((hora - referencia).TotalMinutes) <= 60;

    private static string Direcao(double graus)
    {
        var normalizado = ((graus % 360) + 360) % 360;
        var indice = (int)Math.Round(normalizado / 45) % 8;
        return Direcoes[indice];
    }

    private static string FormatarHoras(double horas)
    {
        var total = Math.Max(0, (int)Math.Round(horas * 60));
        var h = total / 60;
        var m = total % 60;

        return h == 0 ? $"{m} min" : $"{h} h {m:00} min";
    }

    private static string Capitalizar(string texto) =>
        string.IsNullOrWhiteSpace(texto) ? texto : char.ToUpperInvariant(texto[0]) + texto[1..];

    private TimeZoneInfo Fuso()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(opcoes.Fuso);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private sealed record EventoDeMare(string Tipo, DateTimeOffset Hora, double Altura);

    private sealed record ResultadoDoDia(JanelaDto Janela, string Resumo);
}

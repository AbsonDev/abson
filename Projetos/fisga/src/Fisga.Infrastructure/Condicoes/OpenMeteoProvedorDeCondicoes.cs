using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Fisga.Application.Condicoes;
using Fisga.Application.Observabilidade;

namespace Fisga.Infrastructure.Condicoes;

public sealed class OpenMeteoProvedorDeCondicoes(
    HttpClient http,
    HybridCache cache,
    CondicoesOptions opcoes,
    ILogger<OpenMeteoProvedorDeCondicoes> logger,
    ICacheObservabilidade observabilidade) : IProvedorDeCondicoes
{
    private const string Fonte = "Open-Meteo";
    private static readonly TimeSpan TtlMaximo = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan TtlDaLua = TimeSpan.FromHours(12);
    private const int TamanhoMaximoDoTrecho = 200;

    // CacheMinutos só encurta o TTL completo; nunca passa do teto da política.
    private TimeSpan TtlCompleto =>
        TimeSpan.FromMinutes(Math.Clamp(opcoes.CacheMinutos, 1, (int)TtlMaximo.TotalMinutes));

    public async Task<DadosDeCondicoes?> ObterAsync(
        double latitude,
        double longitude,
        DateOnly diaLocal,
        CancellationToken ct = default)
    {
        var chave = ChavesDeCache.Condicoes(latitude, longitude, diaLocal);

        var resultado = await cache.GetOrCreateAsync(
            chave,
            async token =>
            {
                observabilidade.CondicoesUpstream();
                return await CalcularAsync(latitude, longitude, diaLocal, token);
            },
            Opcoes(TtlCompleto),
            cancellationToken: ct);

        // O TTL efetivo depende do valor devolvido, não de quem executou a
        // factory: se o chamador que calculou for cancelado depois de o
        // HybridCache gravar a entrada, um observador ainda corrige o TTL.
        // A escrita usa CancellationToken.None porque é contabilidade de cache,
        // não trabalho do chamador.
        var politica = PoliticaDeCacheDeCondicoes.Ttl(resultado.Dados);
        if (politica < TtlMaximo)
        {
            var restante = Restante(politica, resultado.CalculadoEmTicks);

            if (restante <= TimeSpan.Zero)
            {
                await cache.RemoveAsync(chave, CancellationToken.None);
            }
            else
            {
                await cache.SetAsync(
                    chave,
                    resultado,
                    Opcoes(restante),
                    cancellationToken: CancellationToken.None);
            }
        }

        return resultado.Dados;
    }

    private static HybridCacheEntryOptions Opcoes(TimeSpan ttl) => new()
    {
        Expiration = ttl,
        LocalCacheExpiration = ttl,
    };

    // Idade desde o cálculo: evita que uma leitura reaplique o TTL curto e
    // estenda indefinidamente um resultado parcial/negativo sob tráfego.
    private static TimeSpan Restante(TimeSpan politica, long calculadoEmTicks)
    {
        var idade = DateTimeOffset.UtcNow.UtcTicks - calculadoEmTicks;
        return idade <= 0 ? politica : politica - TimeSpan.FromTicks(idade);
    }

    private async Task<ResultadoDeCondicoes> CalcularAsync(
        double latitude,
        double longitude,
        DateOnly diaLocal,
        CancellationToken ct)
    {
        var climaTask = BuscarClimaAsync(latitude, longitude, ct);
        var marTask = BuscarMarAsync(latitude, longitude, ct);
        var luaTask = ObterLuaAsync(latitude, longitude, diaLocal, ct);
        await Task.WhenAll(climaTask, marTask, luaTask);

        var clima = await climaTask;
        if (clima is null)
        {
            return new ResultadoDeCondicoes(null, Indisponivel: true, CalculadoEmTicks: DateTimeOffset.UtcNow.UtcTicks);
        }

        var mar = await marTask;
        var lua = await luaTask;
        var horas = clima.Horas
            .Select(hora =>
            {
                mar.TryGetValue(hora.Hora, out var dadosMar);
                return hora with
                {
                    NivelDoMarM = dadosMar?.Nivel,
                    TemperaturaDaAguaC = dadosMar?.Temperatura,
                    OndaAlturaM = dadosMar?.Onda,
                    OndaPeriodoS = dadosMar?.Periodo,
                    CorrenteKmh = dadosMar?.Corrente,
                    CorrenteDirecaoGraus = dadosMar?.CorrenteDirecao
                };
            })
            .ToList();

        var dados = new DadosDeCondicoes(
            horas,
            clima.Dias,
            lua,
            horas.Any(hora => hora.NivelDoMarM is not null),
            lua is null ? Fonte : $"{Fonte} + MET Norway");

        return new ResultadoDeCondicoes(
            dados,
            Indisponivel: false,
            CalculadoEmTicks: DateTimeOffset.UtcNow.UtcTicks);
    }

    private async Task<DadosDaLua?> ObterLuaAsync(
        double latitude,
        double longitude,
        DateOnly dia,
        CancellationToken ct)
    {
        var chave = ChavesDeCache.Lua(latitude, longitude, dia);

        var emCache = await cache.GetOrCreateAsync(
            chave,
            async token =>
            {
                var calculado = new LuaEmCache(await BuscarLuaAsync(latitude, longitude, dia, token));
                return calculado;
            },
            Opcoes(TtlDaLua),
            cancellationToken: ct);

        // Falha do MET Norway não fica presa por 12 h: a decisão é pelo valor
        // devolvido, então um observador também remove a entrada quando a
        // factory rodou em outro chamador que foi cancelado.
        if (emCache.Dados is null)
        {
            await cache.RemoveAsync(chave, CancellationToken.None);
        }

        return emCache.Dados;
    }

    private async Task<ClimaBruto?> BuscarClimaAsync(
        double latitude,
        double longitude,
        CancellationToken ct)
    {
        var url = "https://api.open-meteo.com/v1/forecast"
                  + $"?latitude={latitude.ToString(CultureInfo.InvariantCulture)}"
                  + $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}"
                  + "&hourly=temperature_2m,apparent_temperature,precipitation_probability,"
                  + "pressure_msl,wind_speed_10m,wind_gusts_10m,wind_direction_10m,cloud_cover"
                  + "&daily=sunrise,sunset"
                  + "&timezone=America%2FSao_Paulo&forecast_days=8";

        try
        {
            using var resposta = await http.GetAsync(url, ct);
            if (!resposta.IsSuccessStatusCode)
            {
                logger.LogWarning("Open-Meteo recusou o clima: {Status}", resposta.StatusCode);
                return null;
            }

            var corpo = await LerCorpoJsonAsync(resposta, "Open-Meteo clima", ct);
            if (corpo is null)
            {
                return null;
            }

            using var json = JsonDocument.Parse(corpo);
            var raiz = json.RootElement;

            if (!raiz.TryGetProperty("hourly", out var hourly)
                || !hourly.TryGetProperty("time", out var tempo))
            {
                return null;
            }

            var horas = new List<HoraDeCondicoes>();
            var total = tempo.GetArrayLength();

            for (var indice = 0; indice < total; indice++)
            {
                var texto = tempo[indice].GetString();
                if (!TryParseLocal(texto, out var hora))
                {
                    continue;
                }

                horas.Add(new HoraDeCondicoes(
                    hora,
                    Numero(hourly, "temperature_2m", indice),
                    Numero(hourly, "apparent_temperature", indice),
                    Numero(hourly, "precipitation_probability", indice),
                    Numero(hourly, "pressure_msl", indice),
                    Numero(hourly, "wind_speed_10m", indice),
                    Numero(hourly, "wind_gusts_10m", indice),
                    Numero(hourly, "wind_direction_10m", indice),
                    Numero(hourly, "cloud_cover", indice),
                    null,
                    null,
                    null,
                    null));
            }

            var dias = new List<DiaDeCondicoes>();
            if (raiz.TryGetProperty("daily", out var daily)
                && daily.TryGetProperty("time", out var diasJson)
                && daily.TryGetProperty("sunrise", out var nascers)
                && daily.TryGetProperty("sunset", out var pores))
            {
                for (var indice = 0; indice < diasJson.GetArrayLength(); indice++)
                {
                    if (!DateOnly.TryParseExact(
                            diasJson[indice].GetString(),
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var dia))
                    {
                        continue;
                    }

                    if (!TryParseLocal(nascers[indice].GetString(), out var nascer)
                        || !TryParseLocal(pores[indice].GetString(), out var por))
                    {
                        continue;
                    }

                    dias.Add(new DiaDeCondicoes(dia, nascer, por));
                }
            }

            return new ClimaBruto(horas, dias);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excecao) when (excecao is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(excecao, "Falha ao buscar clima em Open-Meteo");
            return null;
        }
    }

    private async Task<Dictionary<DateTimeOffset, DadosDoMar>> BuscarMarAsync(
        double latitude,
        double longitude,
        CancellationToken ct)
    {
        var url = "https://marine-api.open-meteo.com/v1/marine"
                  + $"?latitude={latitude.ToString(CultureInfo.InvariantCulture)}"
                  + $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}"
                  + "&hourly=sea_level_height_msl,sea_surface_temperature,wave_height,wave_period,"
                  + "ocean_current_velocity,ocean_current_direction"
                  + "&timezone=America%2FSao_Paulo&forecast_days=8";

        try
        {
            using var resposta = await http.GetAsync(url, ct);
            if (!resposta.IsSuccessStatusCode)
            {
                return [];
            }

            var corpo = await LerCorpoJsonAsync(resposta, "Open-Meteo mar", ct);
            if (corpo is null)
            {
                return [];
            }

            using var json = JsonDocument.Parse(corpo);
            var raiz = json.RootElement;

            if (!raiz.TryGetProperty("hourly", out var hourly)
                || !hourly.TryGetProperty("time", out var tempo))
            {
                return [];
            }

            var resultado = new Dictionary<DateTimeOffset, DadosDoMar>();
            for (var indice = 0; indice < tempo.GetArrayLength(); indice++)
            {
                if (!TryParseLocal(tempo[indice].GetString(), out var hora))
                {
                    continue;
                }

                var nivel = NumeroOuNulo(hourly, "sea_level_height_msl", indice);
                if (nivel is null)
                {
                    continue;
                }

                resultado[hora] = new DadosDoMar(
                    nivel,
                    NumeroOuNulo(hourly, "sea_surface_temperature", indice),
                    NumeroOuNulo(hourly, "wave_height", indice),
                    NumeroOuNulo(hourly, "wave_period", indice),
                    NumeroOuNulo(hourly, "ocean_current_velocity", indice),
                    NumeroOuNulo(hourly, "ocean_current_direction", indice));
            }

            return resultado;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excecao) when (excecao is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogInformation(excecao, "Maré/ondas indisponíveis no provedor de mar");
            return [];
        }
    }

    private async Task<DadosDaLua?> BuscarLuaAsync(
        double latitude,
        double longitude,
        DateOnly dia,
        CancellationToken ct)
    {
        var hoje = await BuscarLuaDoDiaAsync(latitude, longitude, dia, ct);
        if (hoje is null)
        {
            return null;
        }

        var amanha = await BuscarLuaDoDiaAsync(latitude, longitude, dia.AddDays(1), ct);
        var por = hoje.Por ?? amanha?.Por;

        return new DadosDaLua(
            hoje.Nascer,
            por,
            hoje.Transito,
            hoje.TransitoOposto,
            hoje.FaseAngulo);
    }

    private async Task<LuaBruta?> BuscarLuaDoDiaAsync(
        double latitude,
        double longitude,
        DateOnly dia,
        CancellationToken ct)
    {
        var url = "https://api.met.no/weatherapi/sunrise/3.0/moon"
                  + $"?lat={latitude.ToString(CultureInfo.InvariantCulture)}"
                  + $"&lon={longitude.ToString(CultureInfo.InvariantCulture)}"
                  + $"&date={dia:yyyy-MM-dd}&offset=-03:00";

        try
        {
            using var resposta = await http.GetAsync(url, ct);
            if (!resposta.IsSuccessStatusCode)
            {
                return null;
            }

            var corpo = await LerCorpoJsonAsync(resposta, "MET Norway lua", ct);
            if (corpo is null)
            {
                return null;
            }

            using var json = JsonDocument.Parse(corpo);
            if (!json.RootElement.TryGetProperty("properties", out var propriedades))
            {
                return null;
            }

            var fase = propriedades.TryGetProperty("moonphase", out var angulo)
                       && angulo.ValueKind == JsonValueKind.Number
                ? angulo.GetDouble()
                : 0;

            return new LuaBruta(
                LerTempo(propriedades, "moonrise"),
                LerTempo(propriedades, "moonset"),
                LerTempo(propriedades, "high_moon"),
                LerTempo(propriedades, "low_moon"),
                fase);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excecao) when (excecao is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogInformation(excecao, "Lua indisponível em MET Norway");
            return null;
        }
    }

    // Um 200 nem sempre traz JSON: proxies/CDN do provedor às vezes devolvem
    // texto (ex.: "Upstream ..."). Valida o primeiro byte antes de parsear e
    // registra o que chegou, em vez de estourar JsonException sem contexto.
    private async Task<byte[]?> LerCorpoJsonAsync(
        HttpResponseMessage resposta,
        string fonte,
        CancellationToken ct)
    {
        var corpo = await resposta.Content.ReadAsByteArrayAsync(ct);
        ReadOnlySpan<byte> inicio = corpo;
        if (inicio.StartsWith(Encoding.UTF8.Preamble))
        {
            inicio = inicio[Encoding.UTF8.Preamble.Length..];
        }

        inicio = inicio.TrimStart(" \t\r\n"u8);
        if (!inicio.IsEmpty && (inicio[0] == (byte)'{' || inicio[0] == (byte)'['))
        {
            return corpo;
        }

        var texto = Encoding.UTF8.GetString(corpo);
        var trecho = texto.Length > TamanhoMaximoDoTrecho ? texto[..TamanhoMaximoDoTrecho] : texto;
        logger.LogWarning(
            "{Fonte} devolveu resposta não JSON: {Status} {ContentType} {Trecho}",
            fonte,
            (int)resposta.StatusCode,
            resposta.Content.Headers.ContentType?.ToString() ?? "(sem content-type)",
            trecho);
        return null;
    }

    private static DateTimeOffset? LerTempo(JsonElement propriedades, string nome)
    {
        if (!propriedades.TryGetProperty(nome, out var objeto)
            || !objeto.TryGetProperty("time", out var tempo)
            || tempo.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            tempo.GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var valor)
            ? valor
            : null;
    }

    private static double Numero(JsonElement objeto, string propriedade, int indice)
    {
        if (!objeto.TryGetProperty(propriedade, out var array)
            || array.ValueKind != JsonValueKind.Array
            || indice >= array.GetArrayLength())
        {
            return 0;
        }

        var item = array[indice];
        return item.ValueKind == JsonValueKind.Number ? item.GetDouble() : 0;
    }

    private static double? NumeroOuNulo(JsonElement objeto, string propriedade, int indice)
    {
        if (!objeto.TryGetProperty(propriedade, out var array)
            || array.ValueKind != JsonValueKind.Array
            || indice >= array.GetArrayLength())
        {
            return null;
        }

        var item = array[indice];
        return item.ValueKind == JsonValueKind.Number ? item.GetDouble() : null;
    }

    private static bool TryParseLocal(string? texto, out DateTimeOffset hora)
    {
        hora = default;
        if (string.IsNullOrWhiteSpace(texto)
            || !DateTime.TryParseExact(
                texto,
                "yyyy-MM-ddTHH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var local))
        {
            return false;
        }

        hora = new DateTimeOffset(local, TimeSpan.FromHours(-3));
        return true;
    }

    private sealed record ClimaBruto(
        IReadOnlyList<HoraDeCondicoes> Horas,
        IReadOnlyList<DiaDeCondicoes> Dias);

    private sealed record DadosDoMar(
        double? Nivel,
        double? Temperatura,
        double? Onda,
        double? Periodo,
        double? Corrente,
        double? CorrenteDirecao);

    private sealed record LuaBruta(
        DateTimeOffset? Nascer,
        DateTimeOffset? Por,
        DateTimeOffset? Transito,
        DateTimeOffset? TransitoOposto,
        double FaseAngulo);

    private sealed record ResultadoDeCondicoes(
        DadosDeCondicoes? Dados,
        bool Indisponivel,
        long CalculadoEmTicks);

    private sealed record LuaEmCache(DadosDaLua? Dados);
}

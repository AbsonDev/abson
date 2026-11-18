using System.Globalization;
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
        ResultadoDeCondicoes? recemCalculado = null;

        var resultado = await cache.GetOrCreateAsync(
            chave,
            async token =>
            {
                observabilidade.CondicoesUpstream();
                var calculado = await CalcularAsync(latitude, longitude, diaLocal, token);
                recemCalculado = calculado;
                return calculado;
            },
            cancellationToken: ct);

        // O TTL é escolhido antes da factory rodar; quando o resultado é parcial ou
        // indisponível, a entrada recém-criada é substituída pela validade curta.
        if (recemCalculado is not null)
        {
            var ttl = PoliticaDeCacheDeCondicoes.Ttl(recemCalculado.Dados);

            // Só o resultado completo usa CacheMinutos; parcial e indisponível
            // mantêm os 2 min e os 30 s definidos pela política.
            if (ttl == TtlMaximo)
            {
                ttl = TtlCompleto;
            }

            if (ttl < TtlMaximo)
            {
                await cache.SetAsync(
                    chave,
                    recemCalculado,
                    new HybridCacheEntryOptions
                    {
                        Expiration = ttl,
                        LocalCacheExpiration = ttl,
                    },
                    cancellationToken: ct);
            }
        }

        return resultado.Dados;
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
            return new ResultadoDeCondicoes(null, Indisponivel: true);
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

        return new ResultadoDeCondicoes(dados, Indisponivel: false);
    }

    private async Task<DadosDaLua?> ObterLuaAsync(
        double latitude,
        double longitude,
        DateOnly dia,
        CancellationToken ct)
    {
        var chave = ChavesDeCache.Lua(latitude, longitude, dia);
        LuaEmCache? recemLua = null;

        var emCache = await cache.GetOrCreateAsync(
            chave,
            async token =>
            {
                var calculado = new LuaEmCache(await BuscarLuaAsync(latitude, longitude, dia, token));
                recemLua = calculado;
                return calculado;
            },
            new HybridCacheEntryOptions
            {
                Expiration = TtlDaLua,
                LocalCacheExpiration = TtlDaLua,
            },
            cancellationToken: ct);

        // Falha do MET Norway não fica presa por 12 h: a próxima recomputação tenta de novo.
        if (recemLua is not null && recemLua.Dados is null)
        {
            await cache.RemoveAsync(chave, ct);
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

            await using var fluxo = await resposta.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(fluxo, cancellationToken: ct);
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

            await using var fluxo = await resposta.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(fluxo, cancellationToken: ct);
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

            await using var fluxo = await resposta.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(fluxo, cancellationToken: ct);
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

    private sealed record ResultadoDeCondicoes(DadosDeCondicoes? Dados, bool Indisponivel);

    private sealed record LuaEmCache(DadosDaLua? Dados);
}

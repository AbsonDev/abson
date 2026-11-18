using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Fisga.Application.Condicoes;

namespace Fisga.Infrastructure.Condicoes;

public sealed class OpenMeteoProvedorDeCondicoes(
    HttpClient http,
    ICacheDeCondicoes cache,
    CondicoesOptions opcoes,
    ILogger<OpenMeteoProvedorDeCondicoes> logger) : IProvedorDeCondicoes
{
    private const string Fonte = "Open-Meteo";
    private const int TentativasDeLeitura = 2;
    private const int TamanhoDoTrecho = 200;
    private static readonly TimeSpan EsperaEntreTentativas = TimeSpan.FromMilliseconds(500);

    public async Task<DadosDeCondicoes?> ObterAsync(
        double latitude,
        double longitude,
        DateOnly diaLocal,
        CancellationToken ct = default)
    {
        var chave = $"openmeteo:{Math.Round(latitude, 2):F2}:{Math.Round(longitude, 2):F2}";
        if (cache.TentarObter(chave, out var emCache) && emCache is not null)
        {
            return emCache;
        }

        var clima = await BuscarClimaAsync(latitude, longitude, ct);
        if (clima is null)
        {
            return null;
        }

        var marTask = BuscarMarAsync(latitude, longitude, ct);
        var luaTask = BuscarLuaAsync(latitude, longitude, diaLocal, ct);
        await Task.WhenAll(marTask, luaTask);

        var mar = marTask.Result;
        var lua = luaTask.Result;
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

        cache.Guardar(chave, dados, TimeSpan.FromMinutes(Math.Max(5, opcoes.CacheMinutos)));
        return dados;
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
            var (documento, status) = await LerJsonAsync(url, ct);
            if (documento is null)
            {
                logger.LogWarning("Open-Meteo recusou o clima: {Status}", status);
                return null;
            }

            using var json = documento;
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
            var (documento, _) = await LerJsonAsync(url, ct);
            if (documento is null)
            {
                return [];
            }

            using var json = documento;
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
        catch (Exception excecao) when (excecao is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogInformation(excecao, "Maré/ondas indisponíveis para {Lat},{Lon}", latitude, longitude);
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
            var (documento, _) = await LerJsonAsync(url, ct);
            if (documento is null)
            {
                return null;
            }

            using var json = documento;
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
        catch (Exception excecao) when (excecao is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogInformation(excecao, "Lua indisponível em MET Norway para {Lat},{Lon}", latitude, longitude);
            return null;
        }
    }

    /// <summary>
    /// Faz o GET e devolve o JSON da resposta. Retorna documento nulo (com o status) quando o
    /// provedor responde com erro HTTP. Provedores às vezes respondem 200 com texto puro (página
    /// de erro de proxy/sobrecarga); nesse caso tenta de novo uma vez e, se persistir, lança
    /// <see cref="JsonException"/> com status, content-type e um trecho do corpo para diagnóstico.
    /// </summary>
    private async Task<(JsonDocument? Documento, HttpStatusCode Status)> LerJsonAsync(
        string url,
        CancellationToken ct)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            using var resposta = await http.GetAsync(url, ct);
            if (!resposta.IsSuccessStatusCode)
            {
                return (null, resposta.StatusCode);
            }

            var corpo = await resposta.Content.ReadAsStringAsync(ct);
            if (PareceJson(corpo))
            {
                return (JsonDocument.Parse(corpo), resposta.StatusCode);
            }

            var host = resposta.RequestMessage?.RequestUri?.Host ?? new Uri(url).Host;
            var tipo = resposta.Content.Headers.ContentType?.MediaType ?? "(sem content-type)";
            var trecho = Trecho(corpo);

            if (tentativa < TentativasDeLeitura)
            {
                logger.LogInformation(
                    "Resposta não-JSON de {Host} (status {Status}, {ContentType}); tentando de novo: {Trecho}",
                    host,
                    (int)resposta.StatusCode,
                    tipo,
                    trecho);
                await Task.Delay(EsperaEntreTentativas, ct);
                continue;
            }

            throw new JsonException(
                $"Resposta não-JSON de {host} após {tentativa} tentativas "
                + $"(status {(int)resposta.StatusCode}, content-type {tipo}): {trecho}");
        }
    }

    private static bool PareceJson(string corpo)
    {
        var conteudo = corpo.AsSpan().TrimStart('\uFEFF').TrimStart();
        return conteudo.Length > 0 && (conteudo[0] == '{' || conteudo[0] == '[');
    }

    private static string Trecho(string corpo)
    {
        var limpo = corpo.ReplaceLineEndings(" ").Trim();
        if (limpo.Length == 0)
        {
            return "(corpo vazio)";
        }

        return limpo.Length <= TamanhoDoTrecho ? limpo : limpo[..TamanhoDoTrecho] + "…";
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
}

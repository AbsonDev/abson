using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Fisga.Application.Condicoes;

namespace Fisga.Infrastructure.Condicoes;

public sealed class OpenMeteoGeocodificador(HttpClient http) : IGeocodificador
{
    private static readonly ConcurrentDictionary<string, (DateTimeOffset ExpiraEm, ResultadoDeBusca? Dados)> ReversoCache = new();

    public async Task<IReadOnlyList<ResultadoDeBusca>> BuscarAsync(
        string termo,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(termo) || termo.Trim().Length < 2)
        {
            return [];
        }

        var url = "https://geocoding-api.open-meteo.com/v1/search"
                  + $"?name={Uri.EscapeDataString(termo.Trim())}"
                  + "&count=8&language=pt&format=json&countryCode=BR";

        try
        {
            using var resposta = await http.GetAsync(url, ct);
            if (!resposta.IsSuccessStatusCode)
            {
                return [];
            }

            await using var fluxo = await resposta.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(fluxo, cancellationToken: ct);
            if (!json.RootElement.TryGetProperty("results", out var resultados)
                || resultados.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var lista = new List<ResultadoDeBusca>();
            foreach (var item in resultados.EnumerateArray())
            {
                var nome = Texto(item, "name");
                if (string.IsNullOrWhiteSpace(nome)
                    || !item.TryGetProperty("latitude", out var latitude)
                    || !item.TryGetProperty("longitude", out var longitude)
                    || latitude.ValueKind != JsonValueKind.Number
                    || longitude.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                var estadoNome = Texto(item, "admin1");
                var estado = EstadosBrasileiros.ParaUf(estadoNome)
                             ?? EstadosBrasileiros.ParaUf(Texto(item, "admin2"));

                lista.Add(new ResultadoDeBusca(
                    nome,
                    nome,
                    estado,
                    latitude.GetDouble(),
                    longitude.GetDouble(),
                    "Brasil"));
            }

            return lista
                .DistinctBy(item => (item.Nome, Math.Round(item.Latitude, 2), Math.Round(item.Longitude, 2)))
                .Take(8)
                .ToList();
        }
        catch (Exception excecao) when (excecao is HttpRequestException or TaskCanceledException or JsonException)
        {
            return [];
        }
    }

    public async Task<ResultadoDeBusca?> ReversoAsync(
        double latitude,
        double longitude,
        CancellationToken ct = default)
    {
        var chave = $"{Math.Round(latitude, 3):F3}:{Math.Round(longitude, 3):F3}";
        if (ReversoCache.TryGetValue(chave, out var emCache) && emCache.ExpiraEm > DateTimeOffset.UtcNow)
        {
            return emCache.Dados;
        }

        var url = "https://nominatim.openstreetmap.org/reverse"
                  + $"?format=jsonv2&lat={latitude.ToString(CultureInfo.InvariantCulture)}"
                  + $"&lon={longitude.ToString(CultureInfo.InvariantCulture)}"
                  + "&accept-language=pt-BR&zoom=10&addressdetails=1";

        try
        {
            using var resposta = await http.GetAsync(url, ct);
            if (!resposta.IsSuccessStatusCode)
            {
                ReversoCache[chave] = (DateTimeOffset.UtcNow.AddMinutes(10), null);
                return null;
            }

            await using var fluxo = await resposta.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(fluxo, cancellationToken: ct);
            var raiz = json.RootElement;

            if (!raiz.TryGetProperty("address", out var endereco))
            {
                ReversoCache[chave] = (DateTimeOffset.UtcNow.AddMinutes(10), null);
                return null;
            }

            var cidade = Texto(endereco, "city")
                         ?? Texto(endereco, "town")
                         ?? Texto(endereco, "village")
                         ?? Texto(endereco, "municipality")
                         ?? Texto(endereco, "county");
            var estadoNome = Texto(endereco, "state");
            var uf = EstadosBrasileiros.ParaUf(estadoNome);
            var pais = Texto(endereco, "country");

            if (string.IsNullOrWhiteSpace(cidade) || !string.Equals(pais, "Brasil", StringComparison.OrdinalIgnoreCase))
            {
                ReversoCache[chave] = (DateTimeOffset.UtcNow.AddMinutes(10), null);
                return null;
            }

            var resultado = new ResultadoDeBusca(
                cidade,
                cidade,
                uf,
                latitude,
                longitude,
                pais);

            ReversoCache[chave] = (DateTimeOffset.UtcNow.AddHours(12), resultado);
            return resultado;
        }
        catch (Exception excecao) when (excecao is HttpRequestException or TaskCanceledException or JsonException)
        {
            ReversoCache[chave] = (DateTimeOffset.UtcNow.AddMinutes(5), null);
            return null;
        }
    }

    private static string? Texto(JsonElement item, string propriedade) =>
        item.TryGetProperty(propriedade, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;
}

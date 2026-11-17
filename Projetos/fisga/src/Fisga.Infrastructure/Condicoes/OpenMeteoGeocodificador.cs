using System.Globalization;
using System.Text.Json;
using Fisga.Application.Condicoes;

namespace Fisga.Infrastructure.Condicoes;

public sealed class OpenMeteoGeocodificador(HttpClient http) : IGeocodificador
{
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

    private static string? Texto(JsonElement item, string propriedade) =>
        item.TryGetProperty(propriedade, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;
}

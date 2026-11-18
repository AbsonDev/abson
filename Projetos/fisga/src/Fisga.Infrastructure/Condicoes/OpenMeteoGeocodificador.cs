using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;
using Fisga.Application.Condicoes;
using Fisga.Application.Observabilidade;

namespace Fisga.Infrastructure.Condicoes;

public sealed class OpenMeteoGeocodificador(
    HttpClient http,
    HybridCache cache,
    NominatimThrottle throttle,
    ICacheObservabilidade observabilidade) : IGeocodificador
{
    private static readonly TimeSpan TtlDaBusca = TimeSpan.FromHours(24);
    private static readonly TimeSpan TtlDoReverso = TimeSpan.FromHours(12);
    private static readonly TimeSpan TtlNegativo = TimeSpan.FromMinutes(10);

    public async Task<IReadOnlyList<ResultadoDeBusca>> BuscarAsync(
        string termo,
        CancellationToken ct = default)
    {
        // Um termo curto compartilharia a chave "geocode:search:v1:" e cachearia
        // um resultado que não pertence a nenhuma busca real. A checagem é pelo
        // termo normalizado: acentos combinantes podem reduzir o texto a menos
        // de dois caracteres depois da normalização.
        if (string.IsNullOrWhiteSpace(termo) || termo.Trim().Length < 2)
        {
            return [];
        }

        var termoNormalizado = ChavesDeCache.NormalizarTermo(termo);
        if (termoNormalizado.Length < 2)
        {
            return [];
        }

        var chave = ChavesDeCache.BuscaDeLocalNormalizada(termoNormalizado);

        var emCache = await cache.GetOrCreateAsync(
            chave,
            async token =>
            {
                observabilidade.GeocodingUpstream("busca");
                var lista = await BuscarNoUpstreamAsync(termo.Trim(), token);
                if (lista.Count == 0)
                {
                    observabilidade.GeocodingNegativo("busca");
                }

                return new BuscaEmCache(lista, DateTimeOffset.UtcNow.UtcTicks);
            },
            Opcoes(TtlDaBusca),
            cancellationToken: ct);

        // Busca sem resultados (ou com falha no upstream) não fica 24 h no cache.
        // A política é aplicada pelo valor devolvido; um observador de uma factory
        // cancelada também corrige a entrada.
        if (emCache.Resultados.Count == 0)
        {
            await ReaplicarTtlCurto(chave, emCache, TtlNegativo, emCache.CalculadoEmTicks);
        }

        return emCache.Resultados;
    }

    public async Task<ResultadoDeBusca?> ReversoAsync(
        double latitude,
        double longitude,
        CancellationToken ct = default)
    {
        var chave = ChavesDeCache.Reverso(latitude, longitude);

        var emCache = await cache.GetOrCreateAsync(
            chave,
            async token =>
            {
                // O throttle fica dentro da factory: um cache hit não espera
                // nem consome a cota de 1 req/s do Nominatim. `ExecutarAsync`
                // segura a porta durante o HTTP, então duas chaves de reverso
                // distintas também não se sobrepõem.
                var resultado = await throttle.ExecutarAsync(
                    async executar =>
                    {
                        observabilidade.GeocodingUpstream("reverso");
                        return await BuscarReversoNoUpstreamAsync(latitude, longitude, executar);
                    },
                    token);
                if (resultado is null)
                {
                    observabilidade.GeocodingNegativo("reverso");
                }

                return new ResultadoDeReverso(resultado, DateTimeOffset.UtcNow.UtcTicks);
            },
            Opcoes(TtlDoReverso),
            cancellationToken: ct);

        // Endereço não resolvido não fica 12 h no cache. A política é aplicada
        // pelo valor devolvido, não por quem executou a factory.
        if (emCache.Dados is null)
        {
            await ReaplicarTtlCurto(chave, emCache, TtlNegativo, emCache.CalculadoEmTicks);
        }

        return emCache.Dados;
    }

    private static HybridCacheEntryOptions Opcoes(TimeSpan ttl) => new()
    {
        Expiration = ttl,
        LocalCacheExpiration = ttl,
    };

    // Reaplica a validade curta a partir da idade do cálculo. Sem isso, uma
    // leitura a cada poucos segundos renovaria o TTL curto para sempre.
    private async ValueTask ReaplicarTtlCurto<T>(
        string chave,
        T valor,
        TimeSpan politica,
        long calculadoEmTicks)
    {
        var idade = DateTimeOffset.UtcNow.UtcTicks - calculadoEmTicks;
        var restante = idade <= 0 ? politica : politica - TimeSpan.FromTicks(idade);

        if (restante <= TimeSpan.Zero)
        {
            await cache.RemoveAsync(chave, CancellationToken.None);
        }
        else
        {
            await cache.SetAsync(
                chave,
                valor,
                Opcoes(restante),
                cancellationToken: CancellationToken.None);
        }
    }

    private async Task<List<ResultadoDeBusca>> BuscarNoUpstreamAsync(
        string termo,
        CancellationToken ct)
    {
        var url = "https://geocoding-api.open-meteo.com/v1/search"
                  + $"?name={Uri.EscapeDataString(termo)}"
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
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excecao) when (excecao is HttpRequestException or TaskCanceledException or JsonException)
        {
            return [];
        }
    }

    private async Task<ResultadoDeBusca?> BuscarReversoNoUpstreamAsync(
        double latitude,
        double longitude,
        CancellationToken ct)
    {
        var url = "https://nominatim.openstreetmap.org/reverse"
                  + $"?format=jsonv2&lat={latitude.ToString(CultureInfo.InvariantCulture)}"
                  + $"&lon={longitude.ToString(CultureInfo.InvariantCulture)}"
                  + "&accept-language=pt-BR&zoom=10&addressdetails=1";

        try
        {
            using var resposta = await http.GetAsync(url, ct);
            if (!resposta.IsSuccessStatusCode)
            {
                return null;
            }

            await using var fluxo = await resposta.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(fluxo, cancellationToken: ct);
            var raiz = json.RootElement;

            if (!raiz.TryGetProperty("address", out var endereco))
            {
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
                return null;
            }

            return new ResultadoDeBusca(
                cidade,
                cidade,
                uf,
                latitude,
                longitude,
                pais);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excecao) when (excecao is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    private static string? Texto(JsonElement item, string propriedade) =>
        item.TryGetProperty(propriedade, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;

    private sealed record ResultadoDeReverso(ResultadoDeBusca? Dados, long CalculadoEmTicks);

    private sealed record BuscaEmCache(List<ResultadoDeBusca> Resultados, long CalculadoEmTicks);
}

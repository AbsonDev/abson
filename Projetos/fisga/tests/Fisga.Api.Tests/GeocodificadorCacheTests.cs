using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using Fisga.Application.Condicoes;
using Fisga.Infrastructure;
using Fisga.Infrastructure.Condicoes;
using Fisga.Infrastructure.Observabilidade;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fisga.Api.Tests;

public sealed class GeocodificadorCacheTests
{
    [Fact]
    public async Task Busca_repetida_usa_cache()
    {
        var handler = new ContadorHandler();
        using var http = new HttpClient(handler);
        var geocoder = new OpenMeteoGeocodificador(
            http, CriarHybridCache(), new NominatimThrottle(), new CacheObservabilidadeNula());

        await geocoder.BuscarAsync("Paraty");
        await geocoder.BuscarAsync(" paraty ");

        Assert.Equal(1, handler.ChamadasBusca);
    }

    [Fact]
    public async Task Busca_termo_diferente_consulta_upstream_de_novo()
    {
        var handler = new ContadorHandler();
        using var http = new HttpClient(handler);
        var geocoder = new OpenMeteoGeocodificador(
            http, CriarHybridCache(), new NominatimThrottle(), new CacheObservabilidadeNula());

        await geocoder.BuscarAsync("Paraty");
        await geocoder.BuscarAsync("Angra dos Reis");

        Assert.Equal(2, handler.ChamadasBusca);
    }

    [Fact]
    public async Task Busca_usa_ttl_de_24_horas()
    {
        var handler = new ContadorHandler();
        using var http = new HttpClient(handler);
        var cache = new CacheEspiao(CriarHybridCache());
        var geocoder = new OpenMeteoGeocodificador(
            http, cache, new NominatimThrottle(), new CacheObservabilidadeNula());

        var resultados = await geocoder.BuscarAsync("Paraty");

        var paraty = Assert.Single(resultados);
        Assert.Equal("Paraty", paraty.Nome);
        Assert.Equal("RJ", paraty.Estado);
        Assert.Equal(1, handler.ChamadasBusca);
        Assert.Contains(cache.Criacoes, criacao =>
            criacao.Chave == ChavesDeCache.BuscaDeLocal("Paraty")
            && criacao.Expiration == TimeSpan.FromHours(24)
            && criacao.LocalCacheExpiration == TimeSpan.FromHours(24));
        Assert.Empty(cache.Gravacoes);
    }

    [Fact]
    public async Task Busca_vazia_e_cacheada_negativamente_por_10_min()
    {
        var handler = new ContadorHandler { ResultadoVazio = true };
        using var http = new HttpClient(handler);
        var cache = new CacheEspiao(CriarHybridCache());
        var geocoder = new OpenMeteoGeocodificador(
            http, cache, new NominatimThrottle(), new CacheObservabilidadeNula());

        var primeira = await geocoder.BuscarAsync("Atlantida");
        var segunda = await geocoder.BuscarAsync("Atlantida");

        Assert.Empty(primeira);
        Assert.Empty(segunda);
        Assert.Equal(1, handler.ChamadasBusca);
        AssertGravacoes(
            cache.Gravacoes,
            ChavesDeCache.BuscaDeLocal("Atlantida"),
            TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task Busca_curta_nao_cria_chave_nem_chama_upstream()
    {
        var handler = new ContadorHandler();
        using var http = new HttpClient(handler);
        var cache = new CacheEspiao(CriarHybridCache());
        var geocoder = new OpenMeteoGeocodificador(
            http, cache, new NominatimThrottle(), new CacheObservabilidadeNula());

        string[] termos = ["", " ", "a", " a ", "\u0301\u0301", "\u0301a"];
        foreach (var termo in termos)
        {
            Assert.Empty(await geocoder.BuscarAsync(termo));
        }

        Assert.Equal(0, handler.ChamadasBusca);
        Assert.Empty(cache.Criacoes);
        Assert.Empty(cache.Gravacoes);
    }

    [Fact]
    public async Task Observador_corrige_ttl_negativo_quando_o_calculador_e_cancelado()
    {
        var handler = new ContadorHandler { ResultadoVazio = true };
        using var http = new HttpClient(handler);
        var chave = ChavesDeCache.BuscaDeLocal("Atlantida");
        var cache = new CacheDeCorrida(chave);
        var geocoder = new OpenMeteoGeocodificador(
            http, cache, new NominatimThrottle(), new CacheObservabilidadeNula());

        using var cancelamento = new CancellationTokenSource();
        var calculador = geocoder.BuscarAsync("Atlantida", cancelamento.Token);
        await cache.ValorArmazenado;

        // O observador recebe o valor negativo que a factory já gravou e é ele
        // quem aplica os 10 min, mesmo com o calculador prestes a ser cancelado.
        var observador = await geocoder.BuscarAsync("Atlantida");

        Assert.Empty(observador);
        var gravacao = Assert.Single(cache.Gravacoes, g => g.Chave == chave);
        AssertTtlAproximado(gravacao.Expiration, TimeSpan.FromMinutes(10));

        cancelamento.Cancel();
        cache.Liberar();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => calculador);
    }

    [Fact]
    public async Task Observador_corrige_reverso_negativo_quando_o_calculador_e_cancelado()
    {
        var handler = new ContadorHandler { FalharReverso = true };
        using var http = new HttpClient(handler);
        var chave = ChavesDeCache.Reverso(-23.2178, -44.7131);
        var cache = new CacheDeCorrida(chave);
        var geocoder = new OpenMeteoGeocodificador(
            http, cache, new NominatimThrottle(), new CacheObservabilidadeNula());

        using var cancelamento = new CancellationTokenSource();
        var calculador = geocoder.ReversoAsync(-23.2178, -44.7131, cancelamento.Token);
        await cache.ValorArmazenado;

        var observador = await geocoder.ReversoAsync(-23.2178, -44.7131);

        Assert.Null(observador);
        var gravacao = Assert.Single(cache.Gravacoes, g => g.Chave == chave);
        AssertTtlAproximado(gravacao.Expiration, TimeSpan.FromMinutes(10));

        cancelamento.Cancel();
        cache.Liberar();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => calculador);
    }

    [Fact]
    public async Task Reverso_usa_cache_de_12_horas()
    {
        var handler = new ContadorHandler();
        using var http = new HttpClient(handler);
        var cache = new CacheEspiao(CriarHybridCache());
        var geocoder = new OpenMeteoGeocodificador(
            http, cache, new NominatimThrottle(), new CacheObservabilidadeNula());

        var primeira = await geocoder.ReversoAsync(-23.2178, -44.7131);
        var segunda = await geocoder.ReversoAsync(-23.2178, -44.7131);

        Assert.NotNull(primeira);
        Assert.Equal("Paraty", primeira!.Cidade);
        Assert.Equal("RJ", primeira.Estado);
        Assert.Equal(primeira, segunda);
        Assert.Equal(1, handler.ChamadasReverso);
        Assert.Contains(cache.Criacoes, criacao =>
            criacao.Chave == ChavesDeCache.Reverso(-23.2178, -44.7131)
            && criacao.Expiration == TimeSpan.FromHours(12)
            && criacao.LocalCacheExpiration == TimeSpan.FromHours(12));
        Assert.Empty(cache.Gravacoes);
    }

    [Fact]
    public async Task Reverso_sem_resultado_e_cacheado_negativamente_por_10_min()
    {
        var handler = new ContadorHandler { FalharReverso = true };
        using var http = new HttpClient(handler);
        var cache = new CacheEspiao(CriarHybridCache());
        var geocoder = new OpenMeteoGeocodificador(
            http, cache, new NominatimThrottle(), new CacheObservabilidadeNula());

        var primeira = await geocoder.ReversoAsync(-23.2178, -44.7131);
        var segunda = await geocoder.ReversoAsync(-23.2178, -44.7131);

        Assert.Null(primeira);
        Assert.Null(segunda);
        Assert.Equal(1, handler.ChamadasReverso);
        AssertGravacoes(
            cache.Gravacoes,
            ChavesDeCache.Reverso(-23.2178, -44.7131),
            TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task Reverso_cache_hit_nao_consome_throttle()
    {
        var handler = new ContadorHandler();
        using var http = new HttpClient(handler);
        var geocoder = new OpenMeteoGeocodificador(
            http, CriarHybridCache(), new NominatimThrottle(), new CacheObservabilidadeNula());

        await geocoder.ReversoAsync(-23.2178, -44.7131);

        var relogio = Stopwatch.StartNew();
        await geocoder.ReversoAsync(-23.2178, -44.7131);
        relogio.Stop();

        Assert.Equal(1, handler.ChamadasReverso);
        Assert.True(
            relogio.Elapsed < TimeSpan.FromMilliseconds(500),
            $"cache hit esperou {relogio.Elapsed}; o throttle só deve rodar dentro da factory");
    }

    [Fact]
    public async Task Busca_emite_upstream_so_no_miss()
    {
        var handler = new ContadorHandler();
        using var http = new HttpClient(handler);
        var observabilidade = new CacheObservabilidadeEspiao();
        var geocoder = new OpenMeteoGeocodificador(
            http, CriarHybridCache(), new NominatimThrottle(), observabilidade);

        await geocoder.BuscarAsync("Paraty");
        await geocoder.BuscarAsync("Paraty");

        Assert.Equal("busca", Assert.Single(observabilidade.GeocodingUpstreamChamadas));
        Assert.Empty(observabilidade.GeocodingNegativoChamadas);
    }

    [Fact]
    public async Task Busca_sem_resultado_emite_negativo_de_busca()
    {
        var handler = new ContadorHandler { ResultadoVazio = true };
        using var http = new HttpClient(handler);
        var observabilidade = new CacheObservabilidadeEspiao();
        var geocoder = new OpenMeteoGeocodificador(
            http, CriarHybridCache(), new NominatimThrottle(), observabilidade);

        await geocoder.BuscarAsync("Atlantida");

        Assert.Equal("busca", Assert.Single(observabilidade.GeocodingUpstreamChamadas));
        Assert.Equal("busca", Assert.Single(observabilidade.GeocodingNegativoChamadas));
    }

    [Fact]
    public async Task Reverso_sem_resultado_emite_negativo_de_reverso()
    {
        var handler = new ContadorHandler { FalharReverso = true };
        using var http = new HttpClient(handler);
        var observabilidade = new CacheObservabilidadeEspiao();
        var geocoder = new OpenMeteoGeocodificador(
            http, CriarHybridCache(), new NominatimThrottle(), observabilidade);

        await geocoder.ReversoAsync(-23.2178, -44.7131);

        Assert.Equal("reverso", Assert.Single(observabilidade.GeocodingUpstreamChamadas));
        Assert.Equal("reverso", Assert.Single(observabilidade.GeocodingNegativoChamadas));
    }

    [Fact]
    public async Task Throttle_espaca_inicios_de_chamada_em_um_segundo()
    {
        var throttle = new NominatimThrottle();
        var relogio = Stopwatch.StartNew();
        var inicios = new ConcurrentQueue<TimeSpan>();

        Task<bool> Executar(CancellationToken _)
        {
            inicios.Enqueue(relogio.Elapsed);
            return Task.FromResult(true);
        }

        await Task.WhenAll(
            throttle.ExecutarAsync(Executar),
            throttle.ExecutarAsync(Executar));

        var ordenados = inicios.OrderBy(inicio => inicio).ToArray();
        Assert.Equal(2, ordenados.Length);

        var intervalo = ordenados[1] - ordenados[0];
        Assert.True(
            intervalo >= TimeSpan.FromMilliseconds(900),
            $"duas chamadas seguidas começaram com {intervalo} de intervalo; esperado ao menos 1 s");
    }

    [Fact]
    public async Task Reverso_de_chaves_distintas_nao_sobrepoe_requisicoes()
    {
        var handler = new ConcorrenciaHandler();
        using var http = new HttpClient(handler);
        var geocoder = new OpenMeteoGeocodificador(
            http, CriarHybridCache(), new NominatimThrottle(), new CacheObservabilidadeNula());

        var primeira = geocoder.ReversoAsync(-23.2178, -44.7131);
        var segunda = geocoder.ReversoAsync(-22.9068, -43.1729);
        var resultados = await Task.WhenAll(primeira, segunda);

        Assert.All(resultados, resultado => Assert.NotNull(resultado));
        Assert.Equal(2, handler.Chamadas);
        // O Nominatim pede 1 req/s; sem segurar a porta durante o HTTP, as duas
        // requisições de chaves distintas ficariam em voo ao mesmo tempo.
        Assert.Equal(1, handler.MaximaConcorrencia);
    }

    [Fact]
    public void Infraestrutura_registra_throttle_do_nominatim_como_singleton()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["Database:ConnectionString"] = "Data Source=:memory:",
            })
            .Build();

        var servicos = new ServiceCollection();
        servicos.AdicionarInfraestrutura(configuracao, desenvolvimento: false);

        using var provedor = servicos.BuildServiceProvider();
        var primeiro = provedor.GetRequiredService<NominatimThrottle>();
        var segundo = provedor.GetRequiredService<NominatimThrottle>();

        Assert.Same(primeiro, segundo);
    }

    private static void AssertGravacoes(
        IReadOnlyCollection<ChamadaDeCache> gravacoes,
        string chave,
        TimeSpan esperado)
    {
        var daChave = gravacoes.Where(g => g.Chave == chave).ToArray();
        Assert.NotEmpty(daChave);
        Assert.All(daChave, gravacao =>
        {
            AssertTtlAproximado(gravacao.Expiration, esperado);
            Assert.Equal(gravacao.Expiration, gravacao.LocalCacheExpiration);
        });
    }

    private static void AssertTtlAproximado(TimeSpan? ttl, TimeSpan esperado)
    {
        Assert.NotNull(ttl);
        Assert.True(ttl <= esperado, $"TTL {ttl} maior que o esperado {esperado}");
        Assert.True(
            ttl >= esperado - TimeSpan.FromSeconds(5),
            $"TTL {ttl} muito menor que o esperado {esperado}");
    }

    private static HybridCache CriarHybridCache()
    {
        var servicos = new ServiceCollection();
        servicos.AddHybridCache();
        return servicos.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private sealed class CacheEspiao(HybridCache interno) : HybridCache
    {
        private readonly ConcurrentQueue<ChamadaDeCache> _criacoes = new();
        private readonly ConcurrentQueue<ChamadaDeCache> _gravacoes = new();

        public IReadOnlyCollection<ChamadaDeCache> Criacoes => _criacoes;

        public IReadOnlyCollection<ChamadaDeCache> Gravacoes => _gravacoes;

        public override ValueTask<T> GetOrCreateAsync<TState, T>(
            string key,
            TState state,
            Func<TState, CancellationToken, ValueTask<T>> factory,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            _criacoes.Enqueue(new ChamadaDeCache(
                key, options?.Expiration, options?.LocalCacheExpiration));
            return interno.GetOrCreateAsync(
                key, state, factory, options, tags, cancellationToken);
        }

        public override ValueTask SetAsync<T>(
            string key,
            T value,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            _gravacoes.Enqueue(new ChamadaDeCache(
                key, options?.Expiration, options?.LocalCacheExpiration));
            return interno.SetAsync(key, value, options, tags, cancellationToken);
        }

        public override ValueTask RemoveAsync(
            string key,
            CancellationToken cancellationToken = default) =>
            interno.RemoveAsync(key, cancellationToken);

        public override ValueTask RemoveByTagAsync(
            string tag,
            CancellationToken cancellationToken = default) =>
            interno.RemoveByTagAsync(tag, cancellationToken);
    }

    private sealed record ChamadaDeCache(
        string Chave,
        TimeSpan? Expiration,
        TimeSpan? LocalCacheExpiration);

    private sealed class ConcorrenciaHandler : HttpMessageHandler
    {
        private int _chamadas;
        private int _concorrentes;
        private int _maxima;

        public int Chamadas => Volatile.Read(ref _chamadas);

        public int MaximaConcorrencia => Volatile.Read(ref _maxima);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _chamadas);
            var atuais = Interlocked.Increment(ref _concorrentes);
            AtualizarMaxima(atuais);

            try
            {
                // Precisa durar mais que o intervalo de 1 s: com o throttle
                // antigo (que liberava a porta antes do HTTP), a segunda
                // requisição começaria com a primeira ainda em voo.
                await Task.Delay(1200, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(ReversoJson, Encoding.UTF8, "application/json")
                };
            }
            finally
            {
                Interlocked.Decrement(ref _concorrentes);
            }
        }

        private void AtualizarMaxima(int atuais)
        {
            var maximo = Volatile.Read(ref _maxima);
            while (atuais > maximo)
            {
                var anterior = Interlocked.CompareExchange(ref _maxima, atuais, maximo);
                if (anterior == maximo) return;
                maximo = anterior;
            }
        }
    }

    private sealed class ContadorHandler : HttpMessageHandler
    {
        private int _busca;
        private int _reverso;

        public bool ResultadoVazio { get; init; }

        public bool FalharReverso { get; init; }

        public int ChamadasBusca => Volatile.Read(ref _busca);

        public int ChamadasReverso => Volatile.Read(ref _reverso);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (url.StartsWith("https://geocoding-api.open-meteo.com/v1/search", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _busca);
                return Task.FromResult(Resposta(
                    HttpStatusCode.OK,
                    ResultadoVazio ? "{}" : BuscaJson));
            }

            if (url.StartsWith("https://nominatim.openstreetmap.org/reverse", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _reverso);
                return Task.FromResult(FalharReverso
                    ? Resposta(HttpStatusCode.NotFound, "{}")
                    : Resposta(HttpStatusCode.OK, ReversoJson));
            }

            throw new InvalidOperationException($"URL inesperada no teste: {url}");
        }

        private static HttpResponseMessage Resposta(HttpStatusCode status, string json) => new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private const string BuscaJson = """
        {
          "results": [
            {
              "id": 3456543,
              "name": "Paraty",
              "latitude": -23.2178,
              "longitude": -44.7131,
              "country": "Brasil",
              "admin1": "Rio de Janeiro",
              "admin2": "Paraty"
            }
          ]
        }
        """;

    private const string ReversoJson = """
        {
          "address": {
            "city": "Paraty",
            "state": "Rio de Janeiro",
            "country": "Brasil"
          }
        }
        """;
}

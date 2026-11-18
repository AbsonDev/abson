using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Fisga.Application.Condicoes;
using Fisga.Infrastructure;
using Fisga.Infrastructure.Condicoes;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fisga.Api.Tests;

public sealed class CondicoesCacheTests
{
    private static readonly DateOnly Dia = new(2026, 11, 18);

    [Fact]
    public async Task Condicoes_usa_cache_e_nao_duplica_chamadas_concorrentes()
    {
        var handler = new ContadorHandler();
        using var http = new HttpClient(handler);
        var cache = CriarHybridCache();
        var provedor = new OpenMeteoProvedorDeCondicoes(
            http, cache, new CondicoesOptions(), NullLogger<OpenMeteoProvedorDeCondicoes>.Instance);

        var tarefas = Enumerable.Range(0, 10)
            .Select(_ => provedor.ObterAsync(-23.55, -45.13, Dia))
            .ToArray();
        await Task.WhenAll(tarefas);

        Assert.Equal(1, handler.ChamadasClima);
        Assert.Equal(1, handler.ChamadasMar);
        Assert.All(tarefas, tarefa => Assert.NotNull(tarefa.Result));
        Assert.All(tarefas, tarefa => Assert.True(tarefa.Result!.TemMare));
    }

    [Fact]
    public async Task Condicoes_usa_cache_quente_sem_repetir_upstream()
    {
        var handler = new ContadorHandler();
        using var http = new HttpClient(handler);
        var cache = new CacheEspiao(CriarHybridCache());
        var provedor = new OpenMeteoProvedorDeCondicoes(
            http, cache, new CondicoesOptions(), NullLogger<OpenMeteoProvedorDeCondicoes>.Instance);

        var primeira = await provedor.ObterAsync(-23.55, -45.13, Dia);
        var segunda = await provedor.ObterAsync(-23.55, -45.13, Dia);

        Assert.NotNull(primeira);
        Assert.NotNull(segunda);
        Assert.Equal(1, handler.ChamadasClima);
        Assert.Equal(1, handler.ChamadasMar);
        Assert.Equal(2, handler.ChamadasLua);

        var chaveDeCondicoes = ChavesDeCache.Condicoes(-23.55, -45.13, Dia);
        var chaveDaLua = ChavesDeCache.Lua(-23.55, -45.13, Dia);
        Assert.Contains(cache.Criacoes, criacao =>
            criacao.Chave == chaveDaLua && criacao.Ttl == TimeSpan.FromHours(12));
        Assert.DoesNotContain(cache.Gravacoes, gravacao => gravacao.Chave == chaveDeCondicoes);
    }

    [Fact]
    public async Task Condicoes_falha_de_clima_e_cacheada_negativamente_por_30s()
    {
        var handler = new ContadorHandler { FalharClima = true };
        using var http = new HttpClient(handler);
        var cache = new CacheEspiao(CriarHybridCache());
        var provedor = new OpenMeteoProvedorDeCondicoes(
            http, cache, new CondicoesOptions(), NullLogger<OpenMeteoProvedorDeCondicoes>.Instance);

        var primeira = await provedor.ObterAsync(-23.55, -45.13, Dia);
        var segunda = await provedor.ObterAsync(-23.55, -45.13, Dia);

        Assert.Null(primeira);
        Assert.Null(segunda);
        Assert.Equal(1, handler.ChamadasClima);
        Assert.Contains(cache.Gravacoes, gravacao =>
            gravacao.Chave == ChavesDeCache.Condicoes(-23.55, -45.13, Dia)
            && gravacao.Ttl == TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Condicoes_falha_de_mar_gera_cache_parcial_de_2min()
    {
        var handler = new ContadorHandler { FalharMar = true };
        using var http = new HttpClient(handler);
        var cache = new CacheEspiao(CriarHybridCache());
        var provedor = new OpenMeteoProvedorDeCondicoes(
            http, cache, new CondicoesOptions(), NullLogger<OpenMeteoProvedorDeCondicoes>.Instance);

        var primeira = await provedor.ObterAsync(-23.55, -45.13, Dia);
        var segunda = await provedor.ObterAsync(-23.55, -45.13, Dia);

        Assert.NotNull(primeira);
        Assert.False(primeira!.TemMare);
        Assert.NotNull(segunda);
        Assert.False(segunda!.TemMare);
        Assert.Equal(1, handler.ChamadasMar);
        Assert.Contains(cache.Gravacoes, gravacao =>
            gravacao.Chave == ChavesDeCache.Condicoes(-23.55, -45.13, Dia)
            && gravacao.Ttl == TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task Lua_e_reaproveitada_quando_a_entrada_de_condicoes_expira()
    {
        var handler = new ContadorHandler();
        using var http = new HttpClient(handler);
        var cache = new CacheEspiao(CriarHybridCache());
        var provedor = new OpenMeteoProvedorDeCondicoes(
            http, cache, new CondicoesOptions(), NullLogger<OpenMeteoProvedorDeCondicoes>.Instance);

        await provedor.ObterAsync(-23.55, -45.13, Dia);
        Assert.Equal(2, handler.ChamadasLua);

        var chaveDeCondicoes = ChavesDeCache.Condicoes(-23.55, -45.13, Dia);
        await cache.RemoveAsync(chaveDeCondicoes);
        Assert.Contains(chaveDeCondicoes, cache.Remocoes);

        var segunda = await provedor.ObterAsync(-23.55, -45.13, Dia);

        Assert.NotNull(segunda);
        Assert.Equal(2, handler.ChamadasClima);
        Assert.Equal(2, handler.ChamadasMar);
        Assert.Equal(2, handler.ChamadasLua);
    }

    [Fact]
    public async Task Lua_falha_nao_fica_cacheada_por_12h()
    {
        var handler = new ContadorHandler { FalharLua = true };
        using var http = new HttpClient(handler);
        var cache = new CacheEspiao(CriarHybridCache());
        var provedor = new OpenMeteoProvedorDeCondicoes(
            http, cache, new CondicoesOptions(), NullLogger<OpenMeteoProvedorDeCondicoes>.Instance);

        var primeira = await provedor.ObterAsync(-23.55, -45.13, Dia);
        Assert.NotNull(primeira);
        Assert.Null(primeira!.Lua);
        // BuscarLuaAsync desiste do dia seguinte quando o dia atual falha.
        Assert.Equal(1, handler.ChamadasLua);

        await cache.RemoveAsync(ChavesDeCache.Condicoes(-23.55, -45.13, Dia));

        var segunda = await provedor.ObterAsync(-23.55, -45.13, Dia);
        Assert.NotNull(segunda);
        Assert.Null(segunda!.Lua);
        Assert.Equal(2, handler.ChamadasLua);
    }

    [Fact]
    public async Task Condicoes_cache_minutos_configurado_encurta_o_ttl_completo()
    {
        var handler = new ContadorHandler();
        using var http = new HttpClient(handler);
        var cache = new CacheEspiao(CriarHybridCache());
        var provedor = new OpenMeteoProvedorDeCondicoes(
            http,
            cache,
            new CondicoesOptions { CacheMinutos = 10 },
            NullLogger<OpenMeteoProvedorDeCondicoes>.Instance);

        await provedor.ObterAsync(-23.55, -45.13, Dia);

        Assert.Contains(cache.Gravacoes, gravacao =>
            gravacao.Chave == ChavesDeCache.Condicoes(-23.55, -45.13, Dia)
            && gravacao.Ttl == TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void Infraestrutura_configura_hybrid_cache_com_20_minutos_nos_dois_niveis()
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
        var opcoes = provedor.GetRequiredService<IOptions<HybridCacheOptions>>().Value;

        Assert.NotNull(opcoes.DefaultEntryOptions);
        Assert.Equal(TimeSpan.FromMinutes(20), opcoes.DefaultEntryOptions!.Expiration);
        Assert.Equal(TimeSpan.FromMinutes(20), opcoes.DefaultEntryOptions.LocalCacheExpiration);
    }

    [Fact]
    public async Task Cancelamento_do_cliente_nao_cacheia_indisponibilidade()
    {
        var handler = new ContadorHandler();
        using var http = new HttpClient(handler);
        var cache = new CacheEspiao(CriarHybridCache());
        var provedor = new OpenMeteoProvedorDeCondicoes(
            http, cache, new CondicoesOptions(), NullLogger<OpenMeteoProvedorDeCondicoes>.Instance);

        using var cancelamento = new CancellationTokenSource();
        var tarefa = provedor.ObterAsync(-23.55, -45.13, Dia, cancelamento.Token);
        await handler.Iniciado;
        cancelamento.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tarefa);
        Assert.True(cache.FactoryCancelou);
        Assert.False(cache.FactoryCompletou);
        Assert.Empty(cache.Gravacoes);

        var segunda = await provedor.ObterAsync(-23.55, -45.13, Dia);

        Assert.NotNull(segunda);
        Assert.Equal(1, handler.ChamadasClima);
        Assert.Equal(1, handler.ChamadasMar);
    }

    [Fact]
    public async Task Falhas_de_mar_e_lua_nao_logam_coordenadas()
    {
        var handler = new ContadorHandler { LancarErroMar = true, LancarErroLua = true };
        using var http = new HttpClient(handler);
        var logger = new LoggerEspiao();
        var provedor = new OpenMeteoProvedorDeCondicoes(
            http, CriarHybridCache(), new CondicoesOptions(), logger);

        var resultado = await provedor.ObterAsync(-23.551, -45.134, Dia);

        Assert.NotNull(resultado);
        Assert.False(resultado!.TemMare);
        Assert.Null(resultado.Lua);
        Assert.NotEmpty(logger.Mensagens);
        Assert.All(logger.Mensagens, mensagem =>
        {
            Assert.DoesNotContain("-23.55", mensagem, StringComparison.Ordinal);
            Assert.DoesNotContain("45.13", mensagem, StringComparison.Ordinal);
        });
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
        private readonly ConcurrentQueue<string> _remocoes = new();

        public IReadOnlyCollection<ChamadaDeCache> Criacoes => _criacoes;

        public IReadOnlyCollection<ChamadaDeCache> Gravacoes => _gravacoes;

        public IReadOnlyCollection<string> Remocoes => _remocoes;

        public bool FactoryCompletou { get; private set; }

        public bool FactoryCancelou { get; private set; }

        public override ValueTask<T> GetOrCreateAsync<TState, T>(
            string key,
            TState state,
            Func<TState, CancellationToken, ValueTask<T>> factory,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            _criacoes.Enqueue(new ChamadaDeCache(key, options?.Expiration));
            return interno.GetOrCreateAsync(
                key,
                state,
                async (estado, token) =>
                {
                    try
                    {
                        var valor = await factory(estado, token);
                        FactoryCompletou = true;
                        return valor;
                    }
                    catch (OperationCanceledException)
                    {
                        FactoryCancelou = true;
                        throw;
                    }
                },
                options,
                tags,
                cancellationToken);
        }

        public override ValueTask SetAsync<T>(
            string key,
            T value,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            _gravacoes.Enqueue(new ChamadaDeCache(key, options?.Expiration));
            return interno.SetAsync(key, value, options, tags, cancellationToken);
        }

        public override ValueTask RemoveAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            _remocoes.Enqueue(key);
            return interno.RemoveAsync(key, cancellationToken);
        }

        public override ValueTask RemoveByTagAsync(
            string tag,
            CancellationToken cancellationToken = default) =>
            interno.RemoveByTagAsync(tag, cancellationToken);

        public sealed record ChamadaDeCache(string Chave, TimeSpan? Ttl);
    }

    private sealed class LoggerEspiao : ILogger<OpenMeteoProvedorDeCondicoes>
    {
        private readonly ConcurrentQueue<string> _mensagens = new();

        public IReadOnlyCollection<string> Mensagens => _mensagens;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            _mensagens.Enqueue(formatter(state, exception));
    }

    private sealed class ContadorHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _iniciado =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _clima;
        private int _mar;
        private int _lua;

        public Task Iniciado => _iniciado.Task;

        public bool BloquearAteCancelar { get; init; }

        public bool FalharClima { get; init; }

        public bool FalharMar { get; init; }

        public bool FalharLua { get; init; }

        public bool LancarErroMar { get; init; }

        public bool LancarErroLua { get; init; }

        public int ChamadasClima => Volatile.Read(ref _clima);

        public int ChamadasMar => Volatile.Read(ref _mar);

        public int ChamadasLua => Volatile.Read(ref _lua);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _iniciado.TrySetResult();
            await Task.Delay(
                BloquearAteCancelar ? TimeSpan.FromSeconds(30) : TimeSpan.FromMilliseconds(100),
                cancellationToken);

            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (url.StartsWith("https://api.open-meteo.com/v1/forecast", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _clima);
                return FalharClima
                    ? Resposta(HttpStatusCode.InternalServerError, "{}")
                    : Resposta(HttpStatusCode.OK, ClimaJson);
            }

            if (url.StartsWith("https://marine-api.open-meteo.com/v1/marine", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _mar);
                if (LancarErroMar)
                {
                    throw new HttpRequestException("falha simulada no mar");
                }

                return FalharMar
                    ? Resposta(HttpStatusCode.InternalServerError, "{}")
                    : Resposta(HttpStatusCode.OK, MarJson);
            }

            if (url.StartsWith("https://api.met.no/weatherapi/sunrise/3.0/moon", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _lua);
                if (LancarErroLua)
                {
                    throw new HttpRequestException("falha simulada na lua");
                }

                return FalharLua
                    ? Resposta(HttpStatusCode.InternalServerError, "{}")
                    : Resposta(HttpStatusCode.OK, LuaJson);
            }

            throw new InvalidOperationException($"URL inesperada no teste: {url}");
        }

        private static HttpResponseMessage Resposta(HttpStatusCode status, string json) => new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private const string ClimaJson = """
        {
          "hourly": {
            "time": ["2026-11-18T00:00", "2026-11-18T01:00"],
            "temperature_2m": [24.5, 24.0],
            "apparent_temperature": [25.5, 25.0],
            "precipitation_probability": [10, 20],
            "pressure_msl": [1012.0, 1012.5],
            "wind_speed_10m": [8.0, 9.0],
            "wind_gusts_10m": [14.0, 15.0],
            "wind_direction_10m": [90.0, 100.0],
            "cloud_cover": [40, 50]
          },
          "daily": {
            "time": ["2026-11-18"],
            "sunrise": ["2026-11-18T06:00"],
            "sunset": ["2026-11-18T18:00"]
          }
        }
        """;

    private const string MarJson = """
        {
          "hourly": {
            "time": ["2026-11-18T00:00", "2026-11-18T01:00"],
            "sea_level_height_msl": [0.5, 0.6],
            "sea_surface_temperature": [22.0, 22.1],
            "wave_height": [1.0, 1.2],
            "wave_period": [8.0, 8.5],
            "ocean_current_velocity": [2.0, 2.2],
            "ocean_current_direction": [90.0, 95.0]
          }
        }
        """;

    private const string LuaJson = """
        {
          "properties": {
            "moonrise": { "time": "2026-11-18T05:00:00-03:00" },
            "moonset": { "time": "2026-11-18T18:00:00-03:00" },
            "high_moon": { "time": "2026-11-18T11:00:00-03:00" },
            "low_moon": { "time": "2026-11-18T23:00:00-03:00" },
            "moonphase": 0.5
          }
        }
        """;
}

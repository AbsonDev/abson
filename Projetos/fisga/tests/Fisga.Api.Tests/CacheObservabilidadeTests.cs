using System.Collections.Concurrent;
using Fisga.Api.Observabilidade;
using Fisga.Application.Observabilidade;
using Fisga.Infrastructure;
using Fisga.Infrastructure.Observabilidade;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fisga.Api.Tests;

[Collection("api")]
public sealed class CacheObservabilidadeTests
{
    private readonly ApiTestFixture _fixture;

    public CacheObservabilidadeTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Infraestrutura_registra_observabilidade_nula_por_padrao()
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
        var primeiro = provedor.GetRequiredService<ICacheObservabilidade>();
        var segundo = provedor.GetRequiredService<ICacheObservabilidade>();

        Assert.IsType<CacheObservabilidadeNula>(primeiro);
        Assert.Same(primeiro, segundo);
    }

    [Fact]
    public void Api_substitui_a_implementacao_nula_pelo_adaptador_da_telemetria()
    {
        var observabilidade = _fixture.Factory.Services.GetRequiredService<ICacheObservabilidade>();

        Assert.IsType<SentryCacheObservabilidade>(observabilidade);
    }
}

internal sealed class CacheObservabilidadeEspiao : ICacheObservabilidade
{
    private readonly ConcurrentQueue<string> _geocodingUpstream = new();
    private readonly ConcurrentQueue<string> _geocodingNegativo = new();
    private int _condicoesUpstream;

    public int CondicoesUpstreamChamadas => Volatile.Read(ref _condicoesUpstream);

    public IReadOnlyCollection<string> GeocodingUpstreamChamadas => _geocodingUpstream;

    public IReadOnlyCollection<string> GeocodingNegativoChamadas => _geocodingNegativo;

    public void CondicoesUpstream() => Interlocked.Increment(ref _condicoesUpstream);

    public void GeocodingUpstream(string tipo) => _geocodingUpstream.Enqueue(tipo);

    public void GeocodingNegativo(string tipo) => _geocodingNegativo.Enqueue(tipo);
}

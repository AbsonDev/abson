using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Fisga.Api.Caching;
using Fisga.Api.OpenApi;

namespace Fisga.Api.Tests;

// Invariantes do contrato OpenAPI por operação. O documento é o que a API
// publica em /openapi/v1.json (disponível porque a factory de teste usa
// Development); a expectativa de segurança/cache/rate limit vem da metadata
// real de cada endpoint, para que o contrato não divirja do comportamento.
[Collection("api")]
public sealed class ContratoTests
{
    private static readonly IReadOnlyDictionary<string, int> LimitesPadraoPorMinuto =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["leitura"] = 180,
            ["escrita"] = 40,
            ["auth"] = 8,
            ["pdf"] = 30,
            ["midia"] = 30
        };

    // Operações sem política de rate limit nesta fase; o teste 4 exige que a
    // ausência seja explícita, não que o contrato invente um número.
    private static readonly HashSet<(string Metodo, string Rota)> SemRateLimit =
    [
        ("GET", "/health"),
        ("GET", "/ready"),
        ("POST", "/api/newsletter/enviar"),
        ("GET", "/api/newsletter/preview")
    ];

    // As duas rotas administrativas são AllowAnonymous no código, mas exigem o
    // header X-Admin-Token por checagem manual; o contrato precisa dizer isso.
    private static readonly HashSet<(string Metodo, string Rota)> ComTokenAdmin =
    [
        ("POST", "/api/newsletter/enviar"),
        ("GET", "/api/newsletter/preview")
    ];

    private static Task<JsonDocument>? _documento;

    private readonly ApiTestFixture _fixture;

    public ContratoTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TodaOperacaoTemOperationIdDoMapa()
    {
        var documento = await LoadAsync();
        var operacoes = Operacoes(documento);

        Assert.Equal(54, operacoes.Count);

        var operationIds = new List<string>();
        foreach (var (metodo, rota, operacao) in operacoes)
        {
            var esperado = MapaDeOperacoes.Para(metodo, rota);
            Assert.True(
                esperado is not null,
                $"Operação sem operationId no mapa: {metodo} {rota}");

            Assert.True(
                operacao.TryGetProperty("operationId", out var operationId),
                $"Operação sem operationId no contrato: {metodo} {rota}");
            Assert.Equal(esperado, operationId.GetString());
            Assert.False(
                string.IsNullOrWhiteSpace(operationId.GetString()),
                $"operationId vazio: {metodo} {rota}");

            operationIds.Add(operationId.GetString()!);
        }

        Assert.Equal(operacoes.Count, operationIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(operacoes.Count, MapaDeOperacoes.Todos.Count);
    }

    [Fact]
    public async Task ResumosSaoDeNegocio()
    {
        var documento = await LoadAsync();
        var operacoes = Operacoes(documento);

        Assert.Equal(54, operacoes.Count);

        // Acumula todas as violações em vez de abortar na primeira: o gate de
        // uma tarefa consegue provar que a sua própria área está limpa mesmo
        // enquanto outra área do contrato ainda falha.
        var violacoes = new List<string>();

        foreach (var (metodo, rota, operacao) in operacoes)
        {
            if (!operacao.TryGetProperty("summary", out var resumo))
            {
                violacoes.Add($"Operação sem summary: {metodo} {rota}");
                continue;
            }

            var textoDoResumo = resumo.GetString();
            if (string.IsNullOrWhiteSpace(textoDoResumo))
            {
                violacoes.Add($"summary vazio: {metodo} {rota}");
                continue;
            }

            // Resumo é propósito de negócio, não nome de handler ou assinatura
            // de código copiada para o contrato.
            foreach (var jargao in new[] { "Fisga.Api", "{", "(", "Endpoints" })
            {
                if (textoDoResumo.Contains(jargao, StringComparison.Ordinal))
                {
                    violacoes.Add($"Resumo com jargão de código \"{jargao}\": {metodo} {rota}");
                }
            }
        }

        Assert.Empty(violacoes);
    }

    [Fact]
    public async Task TagsSaoDeDominio()
    {
        var documento = await LoadAsync();
        var operacoes = Operacoes(documento);

        Assert.Equal(13, Tags.Todas.Count);
        Assert.Equal(13, Tags.Todas.Distinct(StringComparer.Ordinal).Count());

        // Acumula todas as violações em vez de abortar na primeira: o gate de
        // uma tarefa consegue provar que a sua própria área está limpa mesmo
        // enquanto outra área do contrato ainda falha.
        var violacoes = new List<string>();
        var tagsUsadas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (metodo, rota, operacao) in operacoes)
        {
            if (!operacao.TryGetProperty("tags", out var tags))
            {
                violacoes.Add($"Operação sem tags: {metodo} {rota}");
                continue;
            }

            if (tags.ValueKind != JsonValueKind.Array)
            {
                violacoes.Add($"Operação deve ter exatamente uma tag de domínio: {metodo} {rota}");
                continue;
            }

            if (tags.GetArrayLength() != 1)
            {
                violacoes.Add($"Operação deve ter exatamente uma tag de domínio: {metodo} {rota}");
            }

            foreach (var tag in tags.EnumerateArray())
            {
                var nome = tag.GetString();
                if (string.IsNullOrWhiteSpace(nome))
                {
                    violacoes.Add($"Tag vazia: {metodo} {rota}");
                    continue;
                }

                if (nome == "Fisga.Api")
                {
                    violacoes.Add($"Tag fora do domínio: {nome} em {metodo} {rota}");
                    continue;
                }

                if (!Tags.Todas.Contains(nome, StringComparer.Ordinal))
                {
                    violacoes.Add($"Tag fora do domínio: {nome} em {metodo} {rota}");
                    continue;
                }

                tagsUsadas.Add(nome);
            }
        }

        // Nenhuma das 13 pode ficar órfã: toda área do domínio tem operação.
        foreach (var tag in Tags.Todas)
        {
            if (!tagsUsadas.Contains(tag))
            {
                violacoes.Add($"Tag órfã sem operação: {tag}");
            }
        }

        Assert.Empty(violacoes);
    }

    [Fact]
    public async Task SegurancaExpressaPorOperacao()
    {
        var documento = await LoadAsync();
        var operacoes = Operacoes(documento);

        // A asserção de contagem impede que os invariantes passem vazios caso
        // o documento publicado perca "paths" mas mantenha os schemes.
        Assert.Equal(54, operacoes.Count);

        foreach (var (metodo, rota, operacao) in operacoes)
        {
            // Chave presente mesmo quando o array é vazio: segurança é decisão
            // explícita por operação, nunca omissão.
            Assert.True(
                operacao.TryGetProperty("security", out var security),
                $"Operação sem security: {metodo} {rota}");
            Assert.Equal(JsonValueKind.Array, security.ValueKind);

            var esquemas = Esquemas(security);

            if (ComTokenAdmin.Contains((metodo, rota)))
            {
                Assert.Contains("adminToken", esquemas);
                Assert.DoesNotContain("cookieAuth", esquemas);
                continue;
            }

            var anonima = Localizar(metodo, rota).Metadata.GetMetadata<IAllowAnonymous>() is not null;
            if (anonima)
            {
                Assert.Empty(esquemas);
            }
            else
            {
                Assert.Contains("cookieAuth", esquemas);
            }
        }

        var schemes = documento.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes");

        Assert.True(
            schemes.TryGetProperty("cookieAuth", out var cookieAuth),
            "components.securitySchemes.cookieAuth ausente");
        Assert.Equal("apiKey", cookieAuth.GetProperty("type").GetString());
        Assert.Equal("cookie", cookieAuth.GetProperty("in").GetString());
        Assert.Equal("fisga_auth", cookieAuth.GetProperty("name").GetString());

        Assert.True(
            schemes.TryGetProperty("adminToken", out var adminToken),
            "components.securitySchemes.adminToken ausente");
        Assert.Equal("apiKey", adminToken.GetProperty("type").GetString());
        Assert.Equal("header", adminToken.GetProperty("in").GetString());
        Assert.Equal("X-Admin-Token", adminToken.GetProperty("name").GetString());
    }

    [Fact]
    public async Task RateLimitECacheRefletemMetadata()
    {
        var documento = await LoadAsync();
        var configuracao = _fixture.Factory.Services.GetRequiredService<IConfiguration>();
        var operacoes = Operacoes(documento);
        var comRateLimit = 0;

        foreach (var (metodo, rota, operacao) in operacoes)
        {
            var endpoint = Localizar(metodo, rota);

            // x-cache é o Cache-Control exato do 2xx; sem política, o
            // default-deny do middleware responde "private, no-store".
            var cacheEsperado = endpoint.Metadata.GetMetadata<CachePolicy>()?.HeaderValue
                ?? CachePolicy.NoStore.HeaderValue;
            Assert.True(
                operacao.TryGetProperty("x-cache", out var cache),
                $"Operação sem x-cache: {metodo} {rota}");
            Assert.Equal(JsonValueKind.String, cache.ValueKind);
            Assert.Equal(cacheEsperado, cache.GetString());

            var politica = endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>();

            if (SemRateLimit.Contains((metodo, rota)))
            {
                Assert.True(
                    politica is null,
                    $"Operação isenta ganhou política de rate limit: {metodo} {rota}");
                Assert.False(
                    operacao.TryGetProperty("x-rate-limit", out _),
                    $"Operação isenta com x-rate-limit: {metodo} {rota}");
                continue;
            }

            comRateLimit++;

            Assert.True(
                politica is not null,
                $"Operação sem política de rate limit: {metodo} {rota}");
            var nomeDaPolitica = politica!.PolicyName;
            Assert.NotNull(nomeDaPolitica);
            Assert.True(
                LimitesPadraoPorMinuto.TryGetValue(nomeDaPolitica, out var limitePadrao),
                $"Política sem limite padrão conhecido: {nomeDaPolitica} ({metodo} {rota})");

            Assert.True(
                operacao.TryGetProperty("x-rate-limit", out var rateLimit),
                $"Operação sem x-rate-limit: {metodo} {rota}");
            Assert.Equal(nomeDaPolitica, rateLimit.GetProperty("policy").GetString());

            var esperado = configuracao.GetValue<int?>($"RateLimiting:{nomeDaPolitica}") ?? limitePadrao;
            Assert.Equal(esperado, rateLimit.GetProperty("requests").GetInt32());
            Assert.Equal(60, rateLimit.GetProperty("windowSeconds").GetInt32());
        }

        Assert.Equal(operacoes.Count - SemRateLimit.Count, comRateLimit);
    }

    /// <summary>
    /// Carrega /openapi/v1.json uma única vez por execução de teste; os quatro
    /// invariantes leem o mesmo documento.
    /// </summary>
    private Task<JsonDocument> LoadAsync() =>
        _documento ??= CarregarContratoAsync();

    private async Task<JsonDocument> CarregarContratoAsync()
    {
        using var cliente = _fixture.Factory.CreateClient();
        using var resposta = await cliente.GetAsync("/openapi/v1.json");
        resposta.EnsureSuccessStatusCode();
        await using var fluxo = await resposta.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(fluxo);
    }

    private static List<(string Metodo, string Rota, JsonElement Operacao)> Operacoes(JsonDocument documento) =>
        documento.RootElement
            .GetProperty("paths")
            .EnumerateObject()
            .SelectMany(caminho => caminho.Value
                .EnumerateObject()
                .Select(metodo => (metodo.Name.ToUpperInvariant(), caminho.Name, metodo.Value)))
            .ToList();

    private static string[] Esquemas(JsonElement security) =>
        security.EnumerateArray()
            .SelectMany(requisito => requisito.EnumerateObject().Select(esquema => esquema.Name))
            .ToArray();

    /// <summary>
    /// Resolve o endpoint real a partir de (método, rota) do contrato. Rota com
    /// restrição ("{id:guid}") é comparada sem a restrição, porque o OpenAPI
    /// publica só o nome do parâmetro.
    /// </summary>
    private RouteEndpoint Localizar(string metodo, string rota)
    {
        var endpoints = _fixture.Factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints;

        var encontrados = endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods
                    .Contains(metodo, StringComparer.OrdinalIgnoreCase) == true
                && NormalizarRota(endpoint.RoutePattern.RawText) == NormalizarRota(rota))
            .ToList();

        return Assert.Single(encontrados);
    }

    private static string NormalizarRota(string? rota)
    {
        if (string.IsNullOrEmpty(rota))
        {
            return "/";
        }

        var semRestricao = Regex.Replace(rota, @"\{([^}:]+):[^}]+\}", "{$1}");
        return "/" + semRestricao.Trim('/');
    }
}

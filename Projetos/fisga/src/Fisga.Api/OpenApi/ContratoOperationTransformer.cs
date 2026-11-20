using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Fisga.Api.Caching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi;

namespace Fisga.Api.OpenApi;

/// <summary>
/// Escreve em cada operação o operationId congelado em <see cref="MapaDeOperacoes"/>,
/// a decisão de segurança derivada da metadata real do endpoint e as extensões
/// <c>x-cache</c>/<c>x-rate-limit</c>. Sem entrada no mapa a geração do documento
/// falha em vez de publicar um operationId gerado pelo framework, que mudaria a
/// cada refatoração de handler.
/// </summary>
public sealed partial class ContratoOperationTransformer(IConfiguration configuration) : IOpenApiOperationTransformer
{
    // Mesmo limite padrão declarado em Program.cs para cada política. A
    // configuração (RateLimiting:<política>) vence quando presente; a tabela é
    // a rede de segurança para o contrato não publicar um número inventado.
    private static readonly IReadOnlyDictionary<string, int> LimitesPadraoPorMinuto =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["leitura"] = 180,
            ["escrita"] = 40,
            ["auth"] = 8,
            ["pdf"] = 30,
            ["midia"] = 30
        };

    // Janela fixa de um minuto em Program.cs (FixedWindowRateLimiterOptions.Window).
    private const int JanelaEmSegundos = 60;

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metodo = context.Description.HttpMethod
            ?? throw new InvalidOperationException(
                $"Operação sem método HTTP no contrato: {context.Description.RelativePath}");

        var rota = NormalizarRota(context.Description.RelativePath);

        operation.OperationId = MapaDeOperacoes.Para(metodo, rota)
            ?? throw new InvalidOperationException($"Operação sem operationId: {metodo} {rota}");

        operation.Security = Seguranca(context, $"{metodo} {rota}");

        EscreverExtensoes(operation, context, $"{metodo} {rota}");

        return Task.CompletedTask;
    }

    // x-cache e x-rate-limit saem exatamente da metadata que o pipeline usa:
    // CacheHeadersMiddleware lê CachePolicy (default-deny NoStore) e
    // UseRateLimiter lê EnableRateLimitingAttribute.
    private void EscreverExtensoes(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        string identificacaoDaOperacao)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();

        var cache = metadata.OfType<CachePolicy>().LastOrDefault()
            ?? CachePolicy.NoStore;
        operation.Extensions["x-cache"] = new JsonNodeExtension(JsonValue.Create(cache.HeaderValue));

        var politica = metadata.OfType<EnableRateLimitingAttribute>().LastOrDefault();
        if (politica is null)
        {
            // Operação isenta: ausência explícita, sem valor inventado.
            return;
        }

        var nome = politica.PolicyName
            ?? throw new InvalidOperationException(
                $"Política de rate limit sem nome: {identificacaoDaOperacao}");

        var requests = configuration.GetValue<int?>($"RateLimiting:{nome}")
            ?? (LimitesPadraoPorMinuto.TryGetValue(nome, out var padrao)
                ? padrao
                : throw new InvalidOperationException(
                    $"Política sem limite conhecido para o contrato: {nome} ({identificacaoDaOperacao})"));

        operation.Extensions["x-rate-limit"] = new JsonNodeExtension(new JsonObject
        {
            ["policy"] = nome,
            ["requests"] = requests,
            ["windowSeconds"] = JanelaEmSegundos
        });
    }

    // A ordem da regra importa: a rota administrativa é AllowAnonymous no
    // pipeline e confere o token no handler, então o marcador precisa vir antes
    // de IAllowAnonymous para não ser publicada como pública.
    private static List<OpenApiSecurityRequirement> Seguranca(
        OpenApiOperationTransformerContext context,
        string identificacaoDaOperacao)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var document = context.Document
            ?? throw new InvalidOperationException(
                $"Operação sem documento para resolver o esquema de segurança: {identificacaoDaOperacao}");

        if (metadata.OfType<RequerTokenAdmin>().Any())
        {
            return [Requisito(Esquemas.AdminToken, document)];
        }

        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            return [];
        }

        return [Requisito(Esquemas.CookieAuth, document)];
    }

    // Referência, não objeto inline: sem ela o JSON traria o scheme completo em
    // vez de {"cookieAuth": []} e o consumidor perderia o vínculo com
    // components.securitySchemes.
    private static OpenApiSecurityRequirement Requisito(string esquema, OpenApiDocument document) =>
        new()
        {
            [new OpenApiSecuritySchemeReference(esquema, document)] = []
        };

    // O OpenAPI publica só o nome do parâmetro, mas a descrição do endpoint pode
    // manter a restrição ("{id:guid}"). A normalização espelha a dos testes.
    private static string NormalizarRota(string? rota)
    {
        if (string.IsNullOrEmpty(rota))
        {
            return "/";
        }

        var semRestricao = RestricaoDeRota().Replace(rota, "{$1}");
        return "/" + semRestricao.Trim('/');
    }

    [GeneratedRegex(@"\{([^}:]+):[^}]+\}")]
    private static partial Regex RestricaoDeRota();
}

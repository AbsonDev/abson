using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Fisga.Api.Caching;
using Fisga.Api.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    // Token da pescaria compartilhada: parâmetro de rota, não scheme. O nome é
    // único no contrato, então a regra não precisa de tabela de rotas.
    private const string ParametroShareToken = "shareToken";

    private const string DescricaoShareToken =
        "Credencial de leitura da pescaria compartilhada: quem tem o token vê o recurso sem sessão.";

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

        DescreverCredencialDeLeitura(operation);

        operation.Security = Seguranca(context, $"{metodo} {rota}");

        EscreverExtensoes(operation, context, $"{metodo} {rota}");

        NormalizarRespostas(operation);

        return Task.CompletedTask;
    }

    // O shareToken é credencial de leitura: o consumidor do contrato precisa
    // saber disso no parâmetro, não no corpo da operação. Só o parâmetro de
    // path com esse nome ganha a descrição; {id:guid} continua sem.
    private static void DescreverCredencialDeLeitura(OpenApiOperation operation)
    {
        if (operation.Parameters is null)
        {
            return;
        }

        foreach (var parametro in operation.Parameters)
        {
            if (parametro.In == ParameterLocation.Path
                && string.Equals(parametro.Name, ParametroShareToken, StringComparison.Ordinal)
                && parametro is OpenApiParameter concreto)
            {
                concreto.Description = DescricaoShareToken;
            }
        }
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

        var requests = PoliticasDeTaxa.LimiteDe(configuration, nome, identificacaoDaOperacao);

        operation.Extensions["x-rate-limit"] = new JsonNodeExtension(new JsonObject
        {
            ["policy"] = nome,
            ["requests"] = requests,
            ["windowSeconds"] = PoliticasDeTaxa.JanelaEmSegundos
        });
    }

    // Respostas são geradas pela metadata Produces<T>; aqui elas ganham o que a
    // metadata não expressa: o espelho do schema de erro em application/json,
    // descrição por status e os headers Location/Retry-After.
    internal static void NormalizarRespostas(OpenApiOperation operation)
    {
        if (operation.Responses is null)
        {
            return;
        }

        foreach (var (codigo, resposta) in operation.Responses)
        {
            if (!int.TryParse(codigo, out var status) || resposta is not OpenApiResponse concreta)
            {
                continue;
            }

            if (status is StatusCodes.Status201Created or StatusCodes.Status202Accepted)
            {
                concreta.Headers ??= new Dictionary<string, IOpenApiHeader>();
                concreta.Headers["Location"] = new OpenApiHeader
                {
                    Description = "URL do recurso criado ou do próximo passo.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri" }
                };
            }

            if (status == StatusCodes.Status429TooManyRequests)
            {
                concreta.Headers ??= new Dictionary<string, IOpenApiHeader>();
                concreta.Headers["Retry-After"] = new OpenApiHeader
                {
                    Description = RejeicaoDoRateLimiter.DescricaoDoHeader,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" }
                };
            }

            if (concreta.Content?.ContainsKey(RespostasPadrao.TipoProblema) == true)
            {
                var problema = concreta.Content[RespostasPadrao.TipoProblema];

                // Cópia, não a mesma instância: editar o espelho em JSON não pode
                // alterar silenciosamente o media type de erro original.
                concreta.Content.TryAdd(
                    RespostasPadrao.TipoJson,
                    new OpenApiMediaType { Schema = problema.Schema, Example = problema.Example });
                concreta.Description = RespostasPadrao.DescricaoComErro(status);
            }
            else if (RespostasPadrao.DescricaoSemSchema(status) is { } descricao)
            {
                concreta.Description = descricao;
            }
        }
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

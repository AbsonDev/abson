using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Fisga.Api.OpenApi;

/// <summary>
/// Escreve em cada operação o operationId congelado em <see cref="MapaDeOperacoes"/>
/// e a decisão de segurança derivada da metadata real do endpoint.
/// Sem entrada no mapa a geração do documento falha em vez de publicar um
/// operationId gerado pelo framework, que mudaria a cada refatoração de handler.
/// </summary>
public sealed partial class ContratoOperationTransformer : IOpenApiOperationTransformer
{
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

        return Task.CompletedTask;
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
            return [Requisito("adminToken", document)];
        }

        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            return [];
        }

        return [Requisito("cookieAuth", document)];
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

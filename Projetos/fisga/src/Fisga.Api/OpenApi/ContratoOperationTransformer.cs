using System.Text.RegularExpressions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Fisga.Api.OpenApi;

/// <summary>
/// Escreve em cada operação o operationId congelado em <see cref="MapaDeOperacoes"/>.
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

        return Task.CompletedTask;
    }

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

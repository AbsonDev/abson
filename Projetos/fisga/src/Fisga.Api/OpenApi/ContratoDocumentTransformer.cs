using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Fisga.Api.OpenApi;

/// <summary>
/// Declara os esquemas de segurança usados pelo contrato. Cada operação aponta
/// para um deles em <c>security</c>; aqui ficam apenas as definições, sem
/// duplicar a regra por operação, que vive em
/// <see cref="ContratoOperationTransformer"/>.
/// </summary>
public sealed class ContratoDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes["cookieAuth"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = "fisga_auth",
            Description = "Sessão do Fisga no cookie `fisga_auth` (`HttpOnly`, `SameSite=Lax`; `Secure` conforme configuração; validade deslizante de 30 dias)."
        };

        document.Components.SecuritySchemes["adminToken"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = "X-Admin-Token",
            Description = "Token administrativo no header `X-Admin-Token`."
        };

        return Task.CompletedTask;
    }
}

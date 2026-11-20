namespace Fisga.Api.OpenApi;

/// <summary>
/// Nomes dos esquemas de segurança do contrato. O
/// <see cref="ContratoDocumentTransformer"/> declara as definições em
/// <c>components.securitySchemes</c>; o <see cref="ContratoOperationTransformer"/>
/// referencia os mesmos ids em <c>security</c>. A constante única evita que os
/// dois lados divirjam numa string solta.
/// </summary>
public static class Esquemas
{
    public const string CookieAuth = "cookieAuth";

    public const string AdminToken = "adminToken";
}

namespace Fisga.Api.OpenApi;

/// <summary>
/// Marca uma operação administrativa que exige o header X-Admin-Token. O token
/// é conferido manualmente no handler — a rota continua <c>AllowAnonymous</c>
/// no pipeline —, mas o contrato precisa declarar o esquema <c>adminToken</c>
/// em vez de anunciá-la como pública ou protegida por cookie.
/// </summary>
public sealed class RequerTokenAdmin;

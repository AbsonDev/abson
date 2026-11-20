namespace Fisga.Api.Contracts;

public sealed record PescariaSalvaResponse(
    Guid Id,
    string ShareToken,
    string Url);

public sealed record MidiaResponse(string Url);

/// <summary>
/// Forma do corpo multipart documentada no contrato. O handler continua lendo o
/// arquivo por <c>Request.Files.GetFile("arquivo")</c>; o tipo existe para o
/// schema publicar o nome do campo e o formato binário.
/// </summary>
public sealed record FormularioDeFoto(Microsoft.AspNetCore.Http.IFormFile Arquivo);

/// <summary>Corpo de <c>GET /health</c>.</summary>
public sealed record SaudeResponse(string Status, string Version);

/// <summary>Corpo de <c>GET /ready</c>.</summary>
public sealed record ProntidaoResponse(string Status);

public sealed record ErroResponse(string Codigo, string Detalhe);

namespace Fisga.Api.Contracts;

public sealed record PescariaSalvaResponse(
    Guid Id,
    string ShareToken,
    string EditToken,
    string Url);

public sealed record MidiaResponse(string Url);

public sealed record ErroResponse(string Codigo, string Detalhe);

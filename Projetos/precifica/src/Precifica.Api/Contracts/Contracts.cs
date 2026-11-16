using Precifica.Application.Calculos;
using Precifica.Application.Pagamentos;

namespace Precifica.Api.Contracts;

public sealed record CalculoSalvoResponse(
    Guid Id,
    string ShareToken,
    string EditToken,
    string Url);

public sealed record CheckoutRequest(string? Email);

public sealed record CheckoutResponse(string Url, Guid Id);

public sealed record PlanosResponse(decimal PdfAvulso, decimal ProMensal, string NomePro, bool Gratuito);

public sealed record WebhookPayload(string? Type, string? Action, WebhookData? Data);

public sealed record WebhookData(string? Id);

public sealed record ErroResponse(string Codigo, string Detalhe);

public sealed record SimulacaoResponse(
    string Tipo,
    string Status,
    decimal Valor,
    string Email,
    string UrlRetorno,
    string? ShareToken);

public sealed record SimulacaoConfirmadaResponse(
    SimulacaoDto Simulacao,
    string UrlRetorno);

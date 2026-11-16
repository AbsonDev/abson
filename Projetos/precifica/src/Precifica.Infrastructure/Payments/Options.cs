namespace Precifica.Infrastructure.Payments;

public sealed class MercadoPagoOptions
{
    public string AccessToken { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.mercadopago.com";

    public string WebhookSecret { get; set; } = string.Empty;
}

public sealed class PagamentoOptions
{
    /// <summary>MercadoPago ou Simulado.</summary>
    public string Modo { get; set; } = "MercadoPago";
}

public sealed class WebOptions
{
    public string Url { get; set; } = "http://localhost:4321";
}

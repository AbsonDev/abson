using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Precifica.Infrastructure.Payments;

namespace Precifica.Infrastructure.Tests;

public sealed class MercadoPagoWebhookTests
{
    private static MercadoPagoGateway CriarGateway(string segredo) =>
        new(
            new HttpClient(),
            Options.Create(new MercadoPagoOptions
            {
                AccessToken = "teste",
                WebhookSecret = segredo
            }),
            NullLogger<MercadoPagoGateway>.Instance);

    [Fact]
    public void Aceita_assinatura_hmac_valida()
    {
        const string segredo = "segredo-de-teste";
        const string dataId = "123456";
        const string requestId = "req-1";
        const string timestamp = "1700000000";

        var manifesto = $"id:{dataId};request-id:{requestId};ts:{timestamp};";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(segredo));
        var assinatura = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(manifesto))).ToLowerInvariant();

        var gateway = CriarGateway(segredo);

        Assert.True(gateway.ValidarWebhook($"ts={timestamp},v1={assinatura}", requestId, dataId));
    }

    [Fact]
    public void Rejeita_assinatura_adulterada_ou_ausente()
    {
        var gateway = CriarGateway("segredo-de-teste");

        Assert.False(gateway.ValidarWebhook("ts=1700000000,v1=deadbeef", "req-1", "123456"));
        Assert.False(gateway.ValidarWebhook(null, "req-1", "123456"));
        Assert.False(gateway.ValidarWebhook("ts=1700000000,v1=deadbeef", null, "123456"));
    }

    [Fact]
    public void Sem_segredo_aceita_notificacao()
    {
        var gateway = CriarGateway(string.Empty);

        Assert.True(gateway.ValidarWebhook(null, null, "123456"));
    }
}

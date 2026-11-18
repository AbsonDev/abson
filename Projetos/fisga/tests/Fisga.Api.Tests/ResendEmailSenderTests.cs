using System.Net;
using Fisga.Application.Identidade;
using Fisga.Infrastructure.Emails;

namespace Fisga.Api.Tests;

public sealed class ResendEmailSenderTests
{
    [Fact]
    public async Task Falha_do_resend_nao_propaga_corpo_com_email()
    {
        const string email = "vitima@exemplo.com";
        using var http = new HttpClient(new RespostaComPii(email));
        var sender = new ResendEmailSender(http, new EmailOptions
        {
            Provider = "Resend",
            ResendApiKey = "chave-de-teste"
        });

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.EnviarAsync(
                email,
                "Confirme seu e-mail no Fisga",
                "<p>html</p>",
                "texto"));

        Assert.DoesNotContain(email, excecao.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("422", excecao.Message);
    }

    private sealed class RespostaComPii(string email) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent(
                    $"{{\"message\":\"destinatário inválido: {email}\",\"name\":\"validation_error\"}}")
            });
    }
}

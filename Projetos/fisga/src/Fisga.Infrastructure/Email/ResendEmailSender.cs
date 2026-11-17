using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fisga.Application.Identidade;

namespace Fisga.Infrastructure.Emails;

public sealed class ResendEmailSender(HttpClient http, EmailOptions opcoes) : IEmailSender
{
    public async Task EnviarLinkDeAcessoAsync(
        string destino,
        string? nome,
        string url,
        DateTimeOffset expiraEm,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(opcoes.ResendApiKey))
        {
            throw new InvalidOperationException(
                "Configure Email:ResendApiKey para enviar e-mails em produção.");
        }

        var mensagem = new
        {
            from = opcoes.From,
            to = new[] { destino },
            subject = "Seu acesso ao Fisga",
            html = ModeloDeEmailDeAcesso.Html(nome, url, expiraEm),
            text = ModeloDeEmailDeAcesso.Texto(nome, url, expiraEm),
            reply_to = string.IsNullOrWhiteSpace(opcoes.ReplyTo) ? null : opcoes.ReplyTo
        };

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(mensagem)
        };
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opcoes.ResendApiKey);

        using var resposta = await http.SendAsync(requisicao, ct);
        if (resposta.IsSuccessStatusCode)
        {
            return;
        }

        var corpo = await resposta.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException(
            $"O Resend recusou o envio ({(int)resposta.StatusCode}): {corpo}");
    }
}

using System.Text.Json;
using Fisga.Application.Identidade;

namespace Fisga.Infrastructure.Emails;

/// <summary>
/// Sender de teste que grava cada e-mail como JSON em <c>Email:OutboxPath</c>.
/// Serve ao E2E com backend real: o teste lê o arquivo para obter o link de
/// confirmação/reset sem expor token em log ou em resposta da API.
/// Só é selecionado quando <c>Email:Provider=Outbox</c>; produção usa Resend e
/// o caminho é vazio por padrão. O outbox contém dados pessoais e deve viver em
/// diretório temporário, nunca versionado.
/// </summary>
public sealed class OutboxEmailSender(EmailOptions opcoes) : IEmailSender
{
    private static readonly JsonSerializerOptions Serializacao = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public Task EnviarLinkDeAcessoAsync(
        string destino,
        string? nome,
        string url,
        DateTimeOffset expiraEm,
        CancellationToken ct = default) =>
        GravarAsync(
            destino,
            "Seu acesso ao Fisga",
            $"<p>Acesse o Fisga pelo link:</p><p><a href=\"{url}\">{url}</a></p>",
            $"Acesse o Fisga pelo link: {url}",
            null,
            ct);

    public Task EnviarAsync(
        string destino,
        string assunto,
        string html,
        string texto,
        string? listUnsubscribeUrl = null,
        CancellationToken ct = default) =>
        GravarAsync(destino, assunto, html, texto, listUnsubscribeUrl, ct);

    private async Task GravarAsync(
        string destino,
        string assunto,
        string? html,
        string texto,
        string? listUnsubscribeUrl,
        CancellationToken ct)
    {
        var pasta = opcoes.OutboxPath;
        if (string.IsNullOrWhiteSpace(pasta))
        {
            throw new InvalidOperationException(
                "Configure Email:OutboxPath para usar Email:Provider=Outbox.");
        }

        Directory.CreateDirectory(pasta);

        var registro = new EmailNoOutbox(
            destino,
            assunto,
            html,
            texto,
            listUnsubscribeUrl,
            DateTimeOffset.UtcNow);

        var arquivo = Path.Combine(
            pasta,
            $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json");

        await File.WriteAllTextAsync(
            arquivo,
            JsonSerializer.Serialize(registro, Serializacao),
            ct);
    }

    private sealed record EmailNoOutbox(
        string Destino,
        string Assunto,
        string? Html,
        string Texto,
        string? ListUnsubscribeUrl,
        DateTimeOffset CriadoEm);
}

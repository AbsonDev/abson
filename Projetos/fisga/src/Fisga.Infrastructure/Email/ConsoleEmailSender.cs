using Microsoft.Extensions.Logging;
using Fisga.Application.Identidade;

namespace Fisga.Infrastructure.Emails;

/// <summary>
/// Sender de desenvolvimento: imprime o link de acesso no log da API para
/// permitir testar o fluxo sem provedor de e-mail.
/// </summary>
public sealed class ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) : IEmailSender
{
    public Task EnviarLinkDeAcessoAsync(
        string destino,
        string? nome,
        string url,
        DateTimeOffset expiraEm,
        CancellationToken ct = default)
    {
        logger.LogWarning(
            """

            ==================== LINK DE ACESSO (DESENVOLVIMENTO) ====================
            Para:   {Destino}
            Expira: {Expira}
            URL:    {Url}
            =========================================================================
            """,
            destino,
            expiraEm.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            url);

        return Task.CompletedTask;
    }

    public Task EnviarAsync(
        string destino,
        string assunto,
        string html,
        string texto,
        string? listUnsubscribeUrl = null,
        CancellationToken ct = default)
    {
        logger.LogWarning(
            """

            ==================== E-MAIL (DESENVOLVIMENTO) ====================
            Para:    {Destino}
            Assunto: {Assunto}
            Baixa:   {Descadastro}
            ------------------------------------------------------------------
            {Texto}
            ==================================================================
            """,
            destino,
            assunto,
            listUnsubscribeUrl ?? "—",
            texto);

        return Task.CompletedTask;
    }
}

using Microsoft.Extensions.Logging;
using Fisga.Application.Common;
using Fisga.Application.Identidade;

namespace Fisga.Infrastructure.Emails;

/// <summary>
/// Sender de desenvolvimento. Por segurança, NÃO registra em log o destinatário,
/// o corpo do e-mail nem URLs com token/uid: apenas metadados seguros (domínio do
/// destinatário, assunto e expiração). O link de acesso continua disponível para
/// testes pela resposta da API (campo <c>linkDesenvolvimento</c> de
/// <c>/api/auth/solicitar</c>).
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
            "Link de acesso (desenvolvimento) enviado para o domínio {Dominio}; expira em {Expira}. " +
            "Use o linkDesenvolvimento da resposta da API para testar.",
            LogSeguro.ParaLog(DominioDe(destino)),
            expiraEm.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));

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
            "E-mail (desenvolvimento) enviado para o domínio {Dominio}; assunto: {Assunto}.",
            LogSeguro.ParaLog(DominioDe(destino)),
            LogSeguro.ParaLog(assunto));

        return Task.CompletedTask;
    }

    private static string DominioDe(string destino)
    {
        if (string.IsNullOrWhiteSpace(destino))
        {
            return "desconhecido";
        }

        var arroba = destino.LastIndexOf('@');
        return arroba >= 0 && arroba < destino.Length - 1
            ? destino[(arroba + 1)..]
            : "desconhecido";
    }
}

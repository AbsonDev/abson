using Fisga.Application.Identidade;

namespace Fisga.Infrastructure.Emails;

/// <summary>
/// Monta e envia os e-mails transacionais de identidade (confirmação de e-mail e
/// redefinição de senha). Centraliza a construção do link <c>uid</c>+<c>token</c> para
/// não duplicar essa lógica entre endpoints. Não registra o token em log: a redação de
/// PII fica por conta do <see cref="IEmailSender"/> (o <c>ConsoleEmailSender</c> já omite
/// url, corpo, token e destinatário completo).
/// </summary>
public sealed class EmailDeIdentidade(IEmailSender email, AutenticacaoOptions opcoes)
{
    private const string AssuntoConfirmacao = "Confirme seu e-mail no Fisga";
    private const string AssuntoRedefinicao = "Redefina sua senha no Fisga";

    public Task EnviarConfirmacaoAsync(
        string destino,
        Guid usuarioId,
        string token,
        CancellationToken ct = default)
    {
        var link = MontarLink($"{opcoes.WebUrl.TrimEnd('/')}/confirmar-email", usuarioId, token);

        return email.EnviarAsync(
            destino,
            AssuntoConfirmacao,
            $"<p>Confirme seu e-mail no Fisga acessando o link:</p><p><a href=\"{link}\">{link}</a></p>",
            $"Confirme seu e-mail no Fisga acessando: {link}",
            ct: ct);
    }

    public Task EnviarRedefinicaoAsync(
        string destino,
        Guid usuarioId,
        string token,
        CancellationToken ct = default)
    {
        var link = MontarLink($"{opcoes.WebUrl.TrimEnd('/')}/redefinir-senha", usuarioId, token);

        return email.EnviarAsync(
            destino,
            AssuntoRedefinicao,
            $"<p>Redefina sua senha no Fisga acessando o link:</p><p><a href=\"{link}\">{link}</a></p>",
            $"Redefina sua senha no Fisga acessando: {link}",
            ct: ct);
    }

    private static string MontarLink(string baseUrl, Guid usuarioId, string token) =>
        $"{baseUrl}?uid={Uri.EscapeDataString(usuarioId.ToString())}&token={Uri.EscapeDataString(token)}";
}

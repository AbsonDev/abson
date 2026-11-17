using System.Globalization;

namespace Fisga.Application.Identidade;

public static class ModeloDeEmailDeAcesso
{
    public static string Html(string? nome, string url, DateTimeOffset expiraEm)
    {
        var saudacao = string.IsNullOrWhiteSpace(nome) ? "Olá, pescador." : $"Olá, {nome.Trim()}.";
        var hora = expiraEm.ToLocalTime().ToString("dd/MM 'às' HH:mm", CultureInfo.GetCultureInfo("pt-BR"));

        return $$"""
        <!doctype html>
        <html lang="pt-BR">
          <body style="margin:0;padding:0;background:#f8f4ea;color:#172a2d;font-family:Inter,Arial,Helvetica,sans-serif;">
            <div style="display:none;max-height:0;overflow:hidden;opacity:0;">Seu link de acesso ao Fisga, válido até {{hora}}.</div>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f8f4ea;padding:32px 16px;">
              <tr>
                <td align="center">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;background:#fffdf8;border:1px solid #e3d8c8;border-radius:18px;padding:36px;">
                    <tr>
                      <td>
                        <p style="margin:0 0 6px;color:#174e57;font-size:22px;font-weight:700;letter-spacing:-0.5px;">fisga</p>
                        <p style="margin:0 0 28px;color:#d9773f;font-size:9px;font-weight:700;letter-spacing:2.4px;text-transform:uppercase;">diário de pesca</p>

                        <h1 style="margin:0 0 12px;color:#174e57;font-family:Georgia,'Times New Roman',serif;font-size:30px;line-height:1.15;font-weight:600;letter-spacing:-0.8px;">Seu acesso ao diário.</h1>
                        <p style="margin:0 0 8px;color:#172a2d;font-size:15px;line-height:1.65;">{{saudacao}}</p>
                        <p style="margin:0 0 26px;color:#657277;font-size:14px;line-height:1.65;">
                          Clique no botão abaixo para entrar no Fisga. O link é de uso único e vale até
                          <strong style="color:#172a2d;">{{hora}}</strong>.
                        </p>

                        <table role="presentation" cellpadding="0" cellspacing="0" style="margin:0 0 26px;">
                          <tr>
                            <td style="border-radius:10px;background:#174e57;">
                              <a href="{{url}}" style="display:inline-block;padding:14px 24px;color:#fff9f0;font-size:14px;font-weight:700;text-decoration:none;border-radius:10px;">Entrar no Fisga</a>
                            </td>
                          </tr>
                        </table>

                        <p style="margin:0 0 6px;color:#657277;font-size:12px;line-height:1.6;">Se o botão não abrir, copie e cole este endereço no navegador:</p>
                        <p style="margin:0 0 28px;word-break:break-all;color:#174e57;font-size:11px;line-height:1.6;">{{url}}</p>

                        <hr style="border:0;border-top:1px solid #e3d8c8;margin:0 0 18px;" />
                        <p style="margin:0;color:#8a9498;font-size:11px;line-height:1.6;">
                          Se você não pediu este acesso, pode ignorar este e-mail. Ninguém entra na sua conta sem este link.
                        </p>
                      </td>
                    </tr>
                  </table>

                  <p style="max-width:560px;margin:18px auto 0;color:#8a9498;font-size:10px;line-height:1.6;text-align:center;">
                    Fisga · cada peixe tem uma história. O Fisga guarda a sua.<br />
                    Você recebeu este e-mail porque alguém pediu um acesso com este endereço.
                  </p>
                </td>
              </tr>
            </table>
          </body>
        </html>
        """;
    }

    public static string Texto(string? nome, string url, DateTimeOffset expiraEm)
    {
        var saudacao = string.IsNullOrWhiteSpace(nome) ? "Olá, pescador." : $"Olá, {nome.Trim()}.";
        var hora = expiraEm.ToLocalTime().ToString("dd/MM 'às' HH:mm", CultureInfo.GetCultureInfo("pt-BR"));

        return $"""
        {saudacao}

        Seu link de acesso ao Fisga é de uso único e vale até {hora}:
        {url}

        Se você não pediu este acesso, ignore este e-mail.
        Fisga · cada peixe tem uma história. O Fisga guarda a sua.
        """;
    }
}

using System.Globalization;
using Fisga.Application.Pescarias;
using Fisga.Domain.Identidade;

namespace Fisga.Application.Newsletter;

public static class ModeloDeNewsletter
{
    public static MensagemDeNewsletter Criar(
        Usuario usuario,
        DiarioDto diario,
        string mesNome,
        IReadOnlyList<LeituraSazonal> leituras,
        string urlDescadastro,
        string webUrl)
    {
        var site = webUrl.TrimEnd('/');
        var nome = usuario.NomeExibicao;
        var assunto = $"Seu mês de pesca no Fisga — {mesNome}";
        var maiorPeso = diario.MaiorPesoKg > 0
            ? $"{Quantidade(diario.MaiorPesoKg)} kg"
            : "—";
        var resumo = diario.TotalPescarias == 0
            ? $"{nome}, seu diário ainda está vazio. Que tal começar pela leitura do mês e registrar a primeira pescaria?"
            : diario.TotalCapturas == 0
                ? $"{nome}, você registrou {diario.TotalPescarias} pescaria(s), mas ainda não anotou capturas. O próximo peixe pode ser o primeiro."
                : $"{nome}, este foi o seu mês de pesca. Veja o que funcionou e o que vale tentar na próxima saída.";
        var insight = diario.Insights.FirstOrDefault(item => item.Rotulo == "Melhor período")
            ?? diario.Insights.FirstOrDefault();

        var insightHtml = insight is null ? string.Empty : $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:0 0 22px;background:#fff9f0;border-left:3px solid #d9773f;border-radius:0 10px 10px 0;">
              <tr><td style="padding:12px 14px;">
                <p style="margin:0;color:#d9773f;font-size:9px;font-weight:700;letter-spacing:1px;text-transform:uppercase;">{insight.Rotulo}</p>
                <p style="margin:3px 0 0;color:#172a2d;font-size:15px;font-weight:700;">{insight.Valor}</p>
                <p style="margin:2px 0 0;color:#657277;font-size:11px;">{insight.Detalhe}</p>
              </td></tr>
            </table>
            """;

        var leiturasHtml = string.Join("", leituras.Select(leitura => $"""
            <tr>
              <td style="padding:10px 0;border-bottom:1px solid #e3d8c8;">
                <a href="{site}{leitura.Url}" style="color:#174e57;font-size:15px;font-weight:700;text-decoration:none;">{leitura.Titulo}</a>
                <p style="margin:4px 0 0;color:#657277;font-size:12px;line-height:1.55;">{leitura.Descricao}</p>
              </td>
            </tr>
            """));

        var html = $$"""
        <!doctype html>
        <html lang="pt-BR">
          <body style="margin:0;padding:0;background:#f8f4ea;color:#172a2d;font-family:Inter,Arial,Helvetica,sans-serif;">
            <div style="display:none;max-height:0;overflow:hidden;opacity:0;">Seu resumo de {{mesNome}}, suas estatísticas e o que pescar agora.</div>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f8f4ea;padding:28px 12px;">
              <tr>
                <td align="center">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;background:#fffdf8;border:1px solid #e3d8c8;border-radius:18px;padding:34px;">
                    <tr>
                      <td>
                        <p style="margin:0 0 4px;color:#174e57;font-family:Georgia,'Times New Roman',serif;font-size:24px;font-weight:700;letter-spacing:-.5px;">fisga</p>
                        <p style="margin:0 0 26px;color:#d9773f;font-size:9px;font-weight:700;letter-spacing:2.4px;text-transform:uppercase;">diário de pesca</p>

                        <p style="margin:0 0 8px;color:#657277;font-size:10px;font-weight:700;letter-spacing:1.8px;text-transform:uppercase;">Seu mês de pesca</p>
                        <h1 style="margin:0 0 10px;color:#174e57;font-family:Georgia,'Times New Roman',serif;font-size:32px;line-height:1.1;font-weight:600;letter-spacing:-.8px;">{{mesNome}} no seu diário.</h1>
                        <p style="margin:0 0 24px;color:#657277;font-size:14px;line-height:1.65;">{{resumo}}</p>

                        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:0 0 22px;">
                          <tr>
                            <td width="25%" style="padding:12px 8px;background:#f8f4ea;border-radius:10px;text-align:center;">
                              <span style="display:block;color:#657277;font-size:9px;font-weight:700;letter-spacing:.8px;text-transform:uppercase;">Pescarias</span>
                              <strong style="display:block;margin-top:4px;color:#174e57;font-family:Georgia,serif;font-size:24px;">{{diario.TotalPescarias}}</strong>
                            </td>
                            <td width="6"></td>
                            <td width="25%" style="padding:12px 8px;background:#f8f4ea;border-radius:10px;text-align:center;">
                              <span style="display:block;color:#657277;font-size:9px;font-weight:700;letter-spacing:.8px;text-transform:uppercase;">Capturas</span>
                              <strong style="display:block;margin-top:4px;color:#174e57;font-family:Georgia,serif;font-size:24px;">{{diario.TotalCapturas}}</strong>
                            </td>
                            <td width="6"></td>
                            <td width="25%" style="padding:12px 8px;background:#f8f4ea;border-radius:10px;text-align:center;">
                              <span style="display:block;color:#657277;font-size:9px;font-weight:700;letter-spacing:.8px;text-transform:uppercase;">Soltos</span>
                              <strong style="display:block;margin-top:4px;color:#56745c;font-family:Georgia,serif;font-size:24px;">{{diario.TotalSoltos}}</strong>
                            </td>
                            <td width="6"></td>
                            <td width="25%" style="padding:12px 8px;background:#f8f4ea;border-radius:10px;text-align:center;">
                              <span style="display:block;color:#657277;font-size:9px;font-weight:700;letter-spacing:.8px;text-transform:uppercase;">Maior peixe</span>
                              <strong style="display:block;margin-top:4px;color:#d9773f;font-family:Georgia,serif;font-size:18px;">{{maiorPeso}}</strong>
                            </td>
                          </tr>
                        </table>

                        {{insightHtml}}

                        <h2 style="margin:0 0 4px;color:#172a2d;font-family:Georgia,serif;font-size:21px;font-weight:600;">O que pescar em {{mesNome}}</h2>
                        <p style="margin:0 0 8px;color:#657277;font-size:12px;">Três leituras rápidas da Wiki do Pescador.</p>
                        <table role="presentation" width="100%" cellpadding="0" cellspacing="0">{{leiturasHtml}}</table>

                        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:22px 0;background:#f8f4ea;border:1px solid #e3d8c8;border-radius:12px;">
                          <tr><td style="padding:14px 16px;">
                            <p style="margin:0 0 3px;color:#172a2d;font-size:12px;font-weight:700;">Antes de sair, confira o defeso.</p>
                            <p style="margin:0;color:#657277;font-size:11px;line-height:1.55;">Tamanhos mínimos e períodos mudam por estado. Consulte a agência ambiental da sua UF. <a href="{{site}}/wiki/defeso" style="color:#174e57;font-weight:700;">Abrir guia</a></p>
                          </td></tr>
                        </table>

                        <table role="presentation" cellpadding="0" cellspacing="0" style="margin:0 0 24px;">
                          <tr>
                            <td style="border-radius:9px;background:#174e57;">
                              <a href="{{site}}/meu-diario" style="display:inline-block;padding:13px 22px;color:#fff9f0;font-size:13px;font-weight:700;text-decoration:none;border-radius:9px;">Abrir meu diário</a>
                            </td>
                            <td width="8"></td>
                            <td style="border:1px solid #e3d8c8;border-radius:9px;">
                              <a href="{{site}}/registrar" style="display:inline-block;padding:12px 20px;color:#174e57;font-size:13px;font-weight:700;text-decoration:none;border-radius:9px;">Registrar pescaria</a>
                            </td>
                          </tr>
                        </table>

                        <hr style="border:0;border-top:1px solid #e3d8c8;margin:0 0 14px;" />
                        <p style="margin:0;color:#8a9498;font-size:10px;line-height:1.6;">
                          Você recebe este resumo porque escolheu receber novidades do Fisga.
                          <a href="{{urlDescadastro}}" style="color:#657277;text-decoration:underline;">Não quero mais receber</a>.
                        </p>
                        <p style="margin:8px 0 0;color:#8a9498;font-size:10px;line-height:1.6;">
                          Fisga · cada peixe tem uma história. O Fisga guarda a sua.
                        </p>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
          </body>
        </html>
        """;

        var leiturasTexto = string.Join(
            "\n",
            leituras.Select(leitura => $"- {leitura.Titulo}: {site}{leitura.Url}"));

        var texto = $"""
        {nome}, este foi o seu mês de pesca no Fisga.

        Pescarias: {diario.TotalPescarias}
        Capturas: {diario.TotalCapturas}
        Soltos: {diario.TotalSoltos}
        Maior peixe: {maiorPeso}

        {(insight is null ? "" : $"{insight.Rotulo}: {insight.Valor} ({insight.Detalhe})")}

        O que pescar em {mesNome}:
        {leiturasTexto}

        Antes de sair, confira o defeso: {site}/wiki/defeso

        Abra seu diário: {site}/meu-diario
        Registre uma pescaria: {site}/registrar

        Não quer mais receber: {urlDescadastro}
        Fisga · cada peixe tem uma história. O Fisga guarda a sua.
        """;

        return new MensagemDeNewsletter(assunto, html, texto);
    }

    private static string Quantidade(decimal valor) =>
        valor.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
}

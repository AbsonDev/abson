using System.Security.Cryptography;
using System.Text;
using Fisga.Application.Identidade;
using Fisga.Application.Newsletter;

namespace Fisga.Api.Endpoints;

public static class NewsletterEndpoints
{
    public static IEndpointRouteBuilder MapNewsletterEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/newsletter/enviar", async (
                HttpContext http,
                ServicoDeNewsletter servico,
                NewsletterOptions opcoes,
                CancellationToken ct) =>
            {
                var token = http.Request.Headers["X-Admin-Token"].ToString();
                if (!TokenValido(token, opcoes.AdminToken))
                {
                    return Results.NotFound();
                }

                var mes = http.Request.Query["mes"].ToString();
                var simulacao = http.Request.Query["dryRun"].ToString() is "1" or "true";
                var resultado = await servico.EnviarAsync(mes, simulacao, ct);
                return Results.Ok(resultado);
            })
            .AllowAnonymous();

        app.MapGet("/api/newsletter/preview", async (
                string? email,
                string? mes,
                HttpContext http,
                ServicoDeNewsletter servico,
                NewsletterOptions opcoes,
                CancellationToken ct) =>
            {
                if (!TokenValido(http.Request.Headers["X-Admin-Token"].ToString(), opcoes.AdminToken))
                {
                    return Results.NotFound();
                }

                var mensagem = await servico.PrevisualizarAsync(email, mes, ct);
                return mensagem is null
                    ? Results.NotFound()
                    : Results.Content(mensagem.Html, "text/html; charset=utf-8");
            })
            .AllowAnonymous();

        app.MapGet("/api/newsletter/descadastrar", async (
                string? token,
                ServicoDeNewsletter servico,
                AutenticacaoOptions auth,
                CancellationToken ct) =>
            {
                var ok = await servico.DescadastrarAsync(token, ct);
                var web = auth.WebUrl.TrimEnd('/');
                return Results.Redirect($"{web}/newsletter/descadastrado?ok={(ok ? "1" : "0")}");
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth");

        return app;
    }

    private static bool TokenValido(string recebido, string esperado)
    {
        if (string.IsNullOrWhiteSpace(recebido) || string.IsNullOrWhiteSpace(esperado))
        {
            return false;
        }

        var a = Encoding.UTF8.GetBytes(recebido.Trim());
        var b = Encoding.UTF8.GetBytes(esperado.Trim());

        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}

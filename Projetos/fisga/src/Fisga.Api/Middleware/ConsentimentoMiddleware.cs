using Fisga.Api.Contracts;
using Fisga.Application.Identidade;

namespace Fisga.Api.Middleware;

/// <summary>
/// Exige o aceite formal dos termos e da privacidade vigentes para rotas de
/// dados. Só atua depois de <see cref="SessaoIdentityMiddleware"/>, quando já
/// há usuário resolvido no HttpContext; usuário não autenticado segue como hoje.
/// O resultado é guardado no HttpContext para <c>/api/auth/me</c> devolver o
/// sinalizador ao frontend.
/// </summary>
public sealed class ConsentimentoMiddleware(RequestDelegate next)
{
    private static readonly (string Metodo, string Caminho)[] Allowlist =
    [
        (HttpMethods.Get, "/api/auth/me"),
        (HttpMethods.Post, "/api/auth/sair"),
        (HttpMethods.Post, "/api/conta/consentimento")
    ];

    public async Task InvokeAsync(HttpContext context, ConsentimentoService consentimento)
    {
        var usuarioId = context.UsuarioId();
        if (usuarioId is null)
        {
            await next(context);
            return;
        }

        var pendente = await consentimento.EstaPendenteAsync(usuarioId.Value, context.RequestAborted);
        context.DefinirConsentimentoPendente(pendente);

        if (!pendente
            || !context.Request.Path.StartsWithSegments("/api")
            || EstaNaAllowlist(context))
        {
            await next(context);
            return;
        }

        await EscreverAsync(context);
    }

    private static bool EstaNaAllowlist(HttpContext context)
    {
        foreach (var (metodo, caminho) in Allowlist)
        {
            if (string.Equals(context.Request.Method, metodo, StringComparison.OrdinalIgnoreCase)
                && string.Equals(context.Request.Path.Value, caminho, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task EscreverAsync(HttpContext context)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(
            new ErroResponse(
                "consentimento_pendente",
                "Aceite os termos e a privacidade vigentes para continuar."));
    }
}

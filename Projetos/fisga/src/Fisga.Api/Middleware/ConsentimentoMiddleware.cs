using Fisga.Api.Contracts;
using Fisga.Api.OpenApi;
using Fisga.Application.Identidade;
using Microsoft.AspNetCore.Authorization;

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
            || PermiteSemConsentimento(context))
        {
            await next(context);
            return;
        }

        await EscreverAsync(context);
    }

    private static bool PermiteSemConsentimento(HttpContext context) =>
        EhAnonimo(context) || EstaNaAllowlist(context);

    /// <summary>
    /// Endpoints marcados com <see cref="IAllowAnonymous"/> não pertencem à área
    /// de dados do usuário: catálogo, login, recuperação, descadastro, PDF
    /// público etc. O endpoint já está resolvido porque o roteamento roda antes.
    /// </summary>
    private static bool EhAnonimo(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null;

    private static bool EstaNaAllowlist(HttpContext context)
    {
        var caminho = context.Request.Path.Value;
        if (caminho is null)
        {
            return false;
        }

        // Remove no máximo UMA barra final: "/api/auth/me/" equivale a
        // "/api/auth/me", mas "/api/auth/me//" e "/api/auth/me/x" continuam
        // bloqueados. A comparação final é exata — sem correspondência por prefixo.
        if (caminho.Length > 1 && caminho[^1] == '/')
        {
            caminho = caminho[..^1];
        }

        foreach (var (metodo, permitido) in Allowlist)
        {
            if (string.Equals(context.Request.Method, metodo, StringComparison.OrdinalIgnoreCase)
                && string.Equals(caminho, permitido, StringComparison.OrdinalIgnoreCase))
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
        await context.Response.WriteAsJsonAsync(
            new ErroResponse(
                CodigosDeErro.ConsentimentoPendente,
                DetalhesDeErro.ConsentimentoPendente),
            options: null,
            contentType: "application/problem+json");
    }
}

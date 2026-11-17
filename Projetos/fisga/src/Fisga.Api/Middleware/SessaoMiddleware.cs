using Fisga.Application.Identidade;

namespace Fisga.Api.Middleware;

public sealed class SessaoMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ServicoDeAutenticacao servico,
        AutenticacaoOptions opcoes)
    {
        // A API só precisa resolver a sessão em rotas /api; mídia e health ficam baratos.
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        if (context.Request.Cookies.TryGetValue(opcoes.CookieName, out var token))
        {
            var sessao = await servico.ValidarSessaoAsync(token, context.RequestAborted);
            if (sessao is not null)
            {
                context.DefinirSessao(sessao.Sessao, sessao.Usuario);
            }
        }

        await next(context);
    }
}

using System.Security.Claims;
using Fisga.Application.Abstractions;
using Fisga.Application.Identidade;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Fisga.Api.Middleware;

/// <summary>
/// Valida, a cada requisição <c>/api</c>, a sessão de dispositivo apontada pelo
/// claim <c>sessao_id</c> do cookie da Identity. Não há cache: a consulta é por
/// chave primária. Uma sessão ausente, revogada, expirada ou de outro usuário
/// invalida o cookie e não define o usuário no HttpContext.
/// </summary>
public sealed class SessaoIdentityMiddleware(RequestDelegate next)
{
    private const string ClaimSessaoId = "sessao_id";

    public async Task InvokeAsync(
        HttpContext context,
        ISessaoRepository sessoes,
        IUsuarioRepository usuarios,
        IUnitOfWork unidade,
        IRelogio relogio)
    {
        if (!context.Request.Path.StartsWithSegments("/api")
            || context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var claim = context.User.FindFirst(ClaimSessaoId)?.Value;
        if (!Guid.TryParse(claim, out var sessaoId))
        {
            // Sem o claim sessao_id não há sessão de dispositivo: a requisição
            // segue sem usuário autenticado. O fallback legado fisga_sessao
            // foi removido na Task 6.
            await next(context);
            return;
        }

        var agora = relogio.Agora;
        var sessao = await sessoes.ObterPorIdAsync(sessaoId, context.RequestAborted);
        var usuarioId = ClaimDeUsuario(context);

        if (sessao is null
            || !sessao.EstaAtiva(agora)
            || usuarioId is null
            || sessao.UsuarioId != usuarioId.Value)
        {
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
            await next(context);
            return;
        }

        // Evita um UPDATE a cada requisição; registra o uso em janelas de 5 minutos.
        if (agora - sessao.UltimoUsoEm > TimeSpan.FromMinutes(5))
        {
            sessao.RegistrarUso(agora);
            await unidade.SalvarAsync(context.RequestAborted);
        }

        var usuario = await usuarios.ObterPorIdAsync(sessao.UsuarioId, context.RequestAborted);
        if (usuario is not null)
        {
            context.DefinirSessao(sessao, usuario);
        }

        await next(context);
    }

    private static Guid? ClaimDeUsuario(HttpContext context)
    {
        var valor = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(valor, out var id) ? id : null;
    }
}

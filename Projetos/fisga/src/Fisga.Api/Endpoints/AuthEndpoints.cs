using Fisga.Api.Contracts;
using Fisga.Api.Observabilidade;
using Fisga.Application.Identidade;
using Fisga.Domain.Common;
using Fisga.Domain.Identidade;

namespace Fisga.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/solicitar", async (
                SolicitarLinkRequest entrada,
                ServicoDeAutenticacao servico,
                IHostEnvironment ambiente,
                CancellationToken ct) =>
            {
                var resultado = await servico.SolicitarLinkAsync(entrada.Email, ct);
                Metricas.LoginSolicitado();
                var link = ambiente.IsDevelopment() && !string.IsNullOrWhiteSpace(resultado.Link)
                    ? resultado.Link
                    : null;

                return Results.Accepted(
                    "/api/auth/me",
                    new SolicitarLinkResponse(
                        "Se este e-mail puder entrar, enviamos um link de acesso. Confira sua caixa de entrada e o spam.",
                        link));
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth");

        app.MapGet("/api/auth/confirmar", async (
                string? token,
                HttpContext http,
                ServicoDeAutenticacao servico,
                AutenticacaoOptions opcoes,
                CancellationToken ct) =>
            {
                var web = opcoes.WebUrl.TrimEnd('/');
                try
                {
                    var sessao = await servico.ConfirmarAsync(
                        token,
                        http.Request.Headers.UserAgent.ToString(),
                        http.Connection.RemoteIpAddress?.ToString(),
                        ct);

                    DefinirCookie(http, sessao.Token, sessao.ExpiraEm, opcoes);
                    Metricas.LoginConfirmado(sessao.PrimeiroAcesso);
                    var destino = sessao.PrimeiroAcesso
                        ? "/boas-vindas?novo=1"
                        : "/entrar/confirmar?estado=ok";
                    return Results.Redirect($"{web}{destino}");
                }
                catch (DomainException excecao)
                {
                    Metricas.LoginFalhou(excecao.Codigo);
                    return Results.Redirect($"{web}/entrar/confirmar?estado={Uri.EscapeDataString(excecao.Codigo)}");
                }
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth");

        app.MapGet("/api/auth/me", (HttpContext http) =>
            {
                var usuario = http.UsuarioAtual();
                return Results.Ok(new MeResponse(
                    usuario is not null,
                    usuario is null ? null : UsuarioResponse.De(usuario)));
            })
            .AllowAnonymous()
            .RequireRateLimiting("leitura");

        app.MapPost("/api/auth/sair", async (
                HttpContext http,
                ServicoDeAutenticacao servico,
                AutenticacaoOptions opcoes,
                CancellationToken ct) =>
            {
                if (http.Request.Cookies.TryGetValue(opcoes.CookieName, out var token))
                {
                    await servico.SairAsync(token, ct);
                }

                LimparCookie(http, opcoes);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .RequireRateLimiting("escrita");

        app.MapPost("/api/auth/sair-de-todos", async (
                HttpContext http,
                ServicoDeAutenticacao servico,
                AutenticacaoOptions opcoes,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                await servico.SairDeTodosAsync(usuarioId, ct);
                LimparCookie(http, opcoes);
                return Results.NoContent();
            })
            .RequireRateLimiting("escrita");

        app.MapPatch("/api/auth/perfil", async (
                AtualizarPerfilRequest entrada,
                HttpContext http,
                ServicoDeAutenticacao servico,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var usuario = await servico.AtualizarPerfilAsync(
                    usuarioId,
                    entrada.Nome,
                    entrada.AceitaNovidades,
                    ct);

                return Results.Ok(UsuarioResponse.De(usuario));
            })
            .RequireRateLimiting("escrita");

        return app;
    }

    public static IEndpointRouteBuilder MapContaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/conta/sessoes", async (
                HttpContext http,
                ServicoDeAutenticacao servico,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var sessoes = await servico.ListarSessoesAsync(usuarioId, ct);
                return Results.Ok(sessoes
                    .Select(sessao => SessaoResponse.De(sessao, http.SessaoAtual()?.Id))
                    .ToList());
            })
            .RequireRateLimiting("leitura");

        app.MapGet("/api/conta/exportar", async (
                HttpContext http,
                ServicoDeConta servico,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var exportacao = await servico.ExportarAsync(usuarioId, ct);
                var conteudo = ServicoDeConta.Serializar(exportacao);
                return Results.File(
                    conteudo,
                    "application/json",
                    $"fisga-conta-{DateTime.UtcNow:yyyyMMdd}.json");
            })
            .RequireRateLimiting("leitura");

        app.MapDelete("/api/conta", async (
                [Microsoft.AspNetCore.Mvc.FromBody] ExcluirContaRequest entrada,
                HttpContext http,
                ServicoDeConta servico,
                AutenticacaoOptions opcoes,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                await servico.ExcluirAsync(usuarioId, entrada.Confirmacao, ct);
                LimparCookie(http, opcoes);
                return Results.NoContent();
            })
            .RequireRateLimiting("escrita");

        return app;
    }

    private static void DefinirCookie(
        HttpContext http,
        string token,
        DateTimeOffset expiraEm,
        AutenticacaoOptions opcoes)
    {
        http.Response.Cookies.Append(opcoes.CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = opcoes.CookieSecure,
            SameSite = SameSiteMode.Lax,
            Expires = expiraEm,
            Path = "/",
            IsEssential = true
        });
    }

    private static void LimparCookie(HttpContext http, AutenticacaoOptions opcoes)
    {
        http.Response.Cookies.Delete(opcoes.CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = opcoes.CookieSecure,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            IsEssential = true
        });
    }
}

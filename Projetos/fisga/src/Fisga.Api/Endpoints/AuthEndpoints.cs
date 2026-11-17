using Microsoft.AspNetCore.Identity;
using Fisga.Api.Contracts;
using Fisga.Api.Observabilidade;
using Fisga.Application.Abstractions;
using Fisga.Application.Identidade;
using Fisga.Domain.Common;
using Fisga.Domain.Identidade;
using Fisga.Infrastructure.Emails;
using Fisga.Infrastructure.Identity;

namespace Fisga.Api.Endpoints;

public static class AuthEndpoints
{
    private const string MensagemCredenciaisInvalidas = "E-mail ou senha incorretos.";
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

        app.MapPost("/api/auth/registrar", async (
                RegistrarRequest entrada,
                ICadastroDeUsuario cadastro,
                UserManager<ApplicationUser> usuarios,
                EmailDeIdentidade emails,
                CancellationToken ct) =>
            {
                var usuario = await cadastro.CriarComSenhaAsync(
                    entrada.Email ?? string.Empty,
                    entrada.Nome,
                    entrada.Senha ?? string.Empty,
                    ct);

                var conta = await usuarios.FindByIdAsync(usuario.Id.ToString())
                    ?? throw new InvalidOperationException("Conta de autenticação não encontrada após o cadastro.");

                var token = await usuarios.GenerateEmailConfirmationTokenAsync(conta);
                await emails.EnviarConfirmacaoAsync(usuario.Email.Valor, usuario.Id, token, ct);

                return Results.Created(
                    "/api/auth/me",
                    new RegistrarResponse("Conta criada. Enviamos um link de confirmação para o seu e-mail."));
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth");

        app.MapPost("/api/auth/entrar", async (
                EntrarRequest entrada,
                SignInManager<ApplicationUser> signIn,
                UserManager<ApplicationUser> usuarios,
                EmailDeIdentidade emails,
                CancellationToken ct) =>
            {
                var email = (entrada.Email ?? string.Empty).Trim();
                var conta = await usuarios.FindByEmailAsync(email);

                if (conta is null)
                {
                    Metricas.LoginFalhou("credenciais_invalidas");
                    return Results.Ok(new EntrarResponse(false, MensagemCredenciaisInvalidas));
                }

                var resultado = await signIn.PasswordSignInAsync(
                    conta,
                    entrada.Senha ?? string.Empty,
                    isPersistent: false,
                    lockoutOnFailure: true);

                if (resultado.Succeeded)
                {
                    Metricas.LoginConfirmado(false);
                    return Results.Ok(new EntrarResponse(true, null));
                }

                if (resultado.IsLockedOut)
                {
                    Metricas.LoginFalhou("bloqueado");
                    return Results.Ok(new EntrarResponse(
                        false,
                        "Muitas tentativas. Tente novamente em alguns minutos."));
                }

                if (resultado.IsNotAllowed)
                {
                    Metricas.LoginFalhou("email_nao_confirmado");

                    // A senha está correta mas o e-mail não foi confirmado: gera um novo
                    // link de confirmação e envia, para não deixar o usuário num beco sem
                    // saída quando o link original expirou.
                    var token = await usuarios.GenerateEmailConfirmationTokenAsync(conta);
                    await emails.EnviarConfirmacaoAsync(conta.Email ?? email, conta.Id, token, ct);

                    return Results.Ok(new EntrarResponse(
                        false,
                        "Confirme seu e-mail para entrar. Enviamos um link de confirmação."));
                }

                Metricas.LoginFalhou("credenciais_invalidas");
                return Results.Ok(new EntrarResponse(false, MensagemCredenciaisInvalidas));
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth");

        app.MapPost("/api/auth/confirmar-email", async (
                ConfirmarEmailRequest entrada,
                UserManager<ApplicationUser> usuarios,
                ICadastroDeUsuario cadastro,
                IUnitOfWork unidade,
                IRelogio relogio,
                CancellationToken ct) =>
            {
                if (!Guid.TryParse(entrada.Uid, out var usuarioId))
                {
                    throw new DomainException(
                        "confirmacao_invalida",
                        "Este link de confirmação é inválido ou expirado.");
                }

                var conta = await usuarios.FindByIdAsync(usuarioId.ToString());
                if (conta is null)
                {
                    throw new DomainException(
                        "confirmacao_invalida",
                        "Este link de confirmação é inválido ou expirado.");
                }

                var resultado = await usuarios.ConfirmEmailAsync(conta, entrada.Token ?? string.Empty);
                if (!resultado.Succeeded)
                {
                    throw new DomainException(
                        "confirmacao_invalida",
                        "Este link de confirmação é inválido ou expirado.");
                }

                var usuario = await cadastro.ObterUsuarioAsync(usuarioId, ct);
                if (usuario is not null)
                {
                    usuario.VerificarEmail(relogio.Agora);
                    await unidade.SalvarAsync(ct);
                }

                return Results.Ok(new ConfirmarEmailResponse("E-mail confirmado. Você já pode entrar."));
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth");

        app.MapPost("/api/auth/esqueci-senha", async (
                EsqueciSenhaRequest entrada,
                UserManager<ApplicationUser> usuarios,
                EmailDeIdentidade emails,
                CancellationToken ct) =>
            {
                const string mensagem = "Se este e-mail estiver cadastrado, enviamos um link para redefinir sua senha.";

                if (string.IsNullOrWhiteSpace(entrada.Email))
                {
                    return Results.Accepted("/api/auth/me", new EsqueciSenhaResponse(mensagem));
                }

                var conta = await usuarios.FindByEmailAsync(entrada.Email.Trim());
                if (conta is not null)
                {
                    var token = await usuarios.GeneratePasswordResetTokenAsync(conta);
                    await emails.EnviarRedefinicaoAsync(
                        conta.Email ?? entrada.Email.Trim(),
                        conta.Id,
                        token,
                        ct);
                    Metricas.ResetSolicitado();
                }

                return Results.Accepted("/api/auth/me", new EsqueciSenhaResponse(mensagem));
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth");

        app.MapPost("/api/auth/redefinir-senha", async (
                RedefinirSenhaRequest entrada,
                UserManager<ApplicationUser> usuarios,
                ISessaoRepository sessoes,
                IUnitOfWork unidade,
                IRelogio relogio,
                CancellationToken ct) =>
            {
                if (!Guid.TryParse(entrada.Uid, out var usuarioId))
                {
                    throw new DomainException(
                        "link_invalido",
                        "Este link de redefinição é inválido ou expirado. Peça um novo.");
                }

                var conta = await usuarios.FindByIdAsync(usuarioId.ToString());
                if (conta is null)
                {
                    throw new DomainException(
                        "link_invalido",
                        "Este link de redefinição é inválido ou expirado. Peça um novo.");
                }

                var senha = entrada.Senha ?? string.Empty;
                if (senha.Length < 10)
                {
                    throw new DomainException("senha_curta", "A senha deve ter pelo menos 10 caracteres.");
                }

                if (senha.Length > 128)
                {
                    throw new DomainException("senha_longa", "A senha deve ter no máximo 128 caracteres.");
                }

                var resultado = await usuarios.ResetPasswordAsync(conta, entrada.Token ?? string.Empty, senha);
                if (!resultado.Succeeded)
                {
                    throw new DomainException(
                        "link_invalido",
                        "Este link de redefinição é inválido ou expirado. Peça um novo.");
                }

                // O SecurityStamp já é rotacionado pelo ResetPasswordAsync; este segundo
                // UpdateSecurityStampAsync torna explícita a invalidação de cookies antigos
                // (constraint global), sem criar infraestrutura de sessão.
                await usuarios.UpdateSecurityStampAsync(conta);

                // Revoga sessões de dispositivo existentes, se houver (Task 5 cria a ponte).
                await sessoes.RevogarTodasAsync(conta.Id, relogio.Agora, exceto: null, ct);
                await unidade.SalvarAsync(ct);

                Metricas.ResetConcluido();
                return Results.Ok(new RedefinirSenhaResponse("Senha redefinida. Você já pode entrar."));
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

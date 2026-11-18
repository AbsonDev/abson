using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
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
    private const string ClaimSessaoId = "sessao_id";
    // Mesma chave usada por SignInManager.ConfigureExternalAuthenticationProperties
    // para guardar o provedor nas propriedades do cookie externo.
    private const string ChaveProvedorExterno = "LoginProvider";
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
                HttpContext http,
                SignInManager<ApplicationUser> signIn,
                UserManager<ApplicationUser> usuarios,
                ICadastroDeUsuario cadastro,
                IdentitySessionService identidade,
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

                var resultado = await signIn.CheckPasswordSignInAsync(
                    conta,
                    entrada.Senha ?? string.Empty,
                    lockoutOnFailure: true);

                if (resultado.Succeeded)
                {
                    var usuario = await cadastro.ObterUsuarioAsync(conta.Id, ct)
                                  ?? throw new InvalidOperationException(
                                      "Usuário de domínio não encontrado para a conta autenticada.");

                    // CheckPasswordSignInAsync só verifica a senha: o cookie é emitido
                    // uma única vez aqui, já com o claim que liga ao dispositivo.
                    await identidade.AssinarAsync(conta, usuario, http, ct);
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
                    // CheckPasswordSignInAsync devolve IsNotAllowed ANTES de verificar a senha
                    // (o PreSignInCheck roda antes do CheckPasswordAsync). Verificamos a
                    // senha aqui para só enviar um novo link quando as credenciais forem
                    // realmente válidas — evita um "email-trigger" não autenticado (spam/
                    // enumeração) em contas não confirmadas.
                    if (await usuarios.CheckPasswordAsync(conta, entrada.Senha ?? string.Empty))
                    {
                        Metricas.LoginFalhou("email_nao_confirmado");

                        var token = await usuarios.GenerateEmailConfirmationTokenAsync(conta);
                        await emails.EnviarConfirmacaoAsync(conta.Email ?? email, conta.Id, token, ct);

                        return Results.Ok(new EntrarResponse(
                            false,
                            "Confirme seu e-mail para entrar. Enviamos um link de confirmação."));
                    }

                    Metricas.LoginFalhou("credenciais_invalidas");
                    return Results.Ok(new EntrarResponse(false, MensagemCredenciaisInvalidas));
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
                UserManager<ApplicationUser> usuarios,
                IdentitySessionService identidade,
                AutenticacaoOptions opcoes,
                CancellationToken ct) =>
            {
                var web = opcoes.WebUrl.TrimEnd('/');
                try
                {
                    var resultado = await servico.ConfirmarAsync(token, ct);

                    var conta = await usuarios.FindByIdAsync(resultado.Usuario.Id.ToString())
                                ?? throw new InvalidOperationException(
                                    "Conta de autenticação não encontrada após a confirmação do link.");

                    // O cookie da Identity é a única autenticação; a sessão de
                    // dispositivo é criada aqui, ligada ao cookie pelo claim sessao_id.
                    await identidade.AssinarAsync(conta, resultado.Usuario, http, ct);

                    Metricas.LoginConfirmado(resultado.PrimeiroAcesso);
                    var destino = resultado.PrimeiroAcesso
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

        app.MapGet("/api/auth/google/disponivel", (IConfiguration configuration) =>
                Results.Ok(new GoogleDisponivelResponse(GoogleConfigurado(configuration))))
            .AllowAnonymous()
            .RequireRateLimiting("auth");

        app.MapGet("/api/auth/google/iniciar", (IConfiguration configuration) =>
            {
                if (!GoogleConfigurado(configuration))
                {
                    Metricas.LoginFalhou("google_indisponivel");
                    return Results.Json(
                        new ErroResponse(
                            "google_indisponivel",
                            "O login com Google não está configurado."),
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        contentType: "application/problem+json");
                }

                var propriedades = new AuthenticationProperties
                {
                    // O handler intercepta /signin-google e devolve aqui, já com o
                    // cookie do esquema externo assinado.
                    RedirectUri = "/api/auth/google/concluir"
                };
                propriedades.Items[ChaveProvedorExterno] = GoogleIdentityValidator.Provider;

                return Results.Challenge(propriedades, [GoogleDefaults.AuthenticationScheme]);
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth");

        app.MapGet("/api/auth/google/concluir", async (
                HttpContext http,
                GoogleIdentityValidator validator,
                GoogleIdentityService servico,
                UserManager<ApplicationUser> usuarios,
                IdentitySessionService identidade,
                IUnitOfWork unidade,
                IRelogio relogio,
                AutenticacaoOptions opcoes,
                CancellationToken ct) =>
            {
                var web = opcoes.WebUrl.TrimEnd('/');
                var autenticacao = await http.AuthenticateAsync(IdentityConstants.ExternalScheme);
                if (autenticacao is null || !autenticacao.Succeeded || autenticacao.Principal is null)
                {
                    Metricas.LoginFalhou("google_sessao_externa_invalida");
                    return Results.Redirect($"{web}/entrar/confirmar?estado=google_sessao_externa_invalida");
                }

                var provedor = GoogleIdentityValidator.Provider;
                if (autenticacao.Properties?.Items.TryGetValue(ChaveProvedorExterno, out var salvo) == true
                    && !string.IsNullOrWhiteSpace(salvo))
                {
                    provedor = salvo;
                }

                var subject = autenticacao.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? autenticacao.Principal.FindFirst("sub")?.Value
                              ?? string.Empty;
                var info = new ExternalLoginInfo(
                    autenticacao.Principal, provedor, subject, GoogleIdentityValidator.Provider);

                try
                {
                    var google = await validator.ValidarAsync(info, ct);
                    var usuario = await servico.EntrarOuCriarAsync(google, ct);
                    var conta = await usuarios.FindByIdAsync(usuario.Id.ToString())
                                ?? throw new DomainException(
                                    "google_conta_inconsistente",
                                    "Não foi possível localizar a conta autenticada.");

                    // Espelha o link mágico: primeiro acesso leva às boas-vindas.
                    var primeiroAcesso = usuario.BemVindoEm is null;
                    var agora = relogio.Agora;
                    usuario.VerificarEmail(agora);
                    usuario.RegistrarAcesso(agora);
                    usuario.MarcarBoasVindas(agora);
                    await unidade.SalvarAsync(ct);

                    await identidade.AssinarAsync(conta, usuario, http, ct);
                    await http.SignOutAsync(IdentityConstants.ExternalScheme);

                    Metricas.LoginConfirmado(primeiroAcesso);
                    var destino = primeiroAcesso
                        ? "/boas-vindas?novo=1"
                        : "/entrar/confirmar?estado=ok";
                    return Results.Redirect($"{web}{destino}");
                }
                catch (DomainException excecao)
                {
                    await http.SignOutAsync(IdentityConstants.ExternalScheme);
                    Metricas.LoginFalhou(excecao.Codigo);
                    return Results.Redirect(
                        $"{web}/entrar/confirmar?estado={Uri.EscapeDataString(excecao.Codigo)}");
                }
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth");

        app.MapGet("/api/auth/me", (HttpContext http) =>
            {
                var usuario = http.UsuarioAtual();
                return Results.Ok(new MeResponse(
                    usuario is not null,
                    usuario is null ? null : UsuarioResponse.De(usuario),
                    http.ConsentimentoPendente()));
            })
            .AllowAnonymous()
            .RequireRateLimiting("leitura");

        app.MapPost("/api/auth/sair", async (
                HttpContext http,
                ISessaoRepository sessoes,
                IUnitOfWork unidade,
                IRelogio relogio,
                AutenticacaoOptions opcoes,
                CancellationToken ct) =>
            {
                var claim = http.User.FindFirst(ClaimSessaoId)?.Value;
                if (http.User.Identity?.IsAuthenticated == true
                    && Guid.TryParse(claim, out var sessaoId)
                    && http.UsuarioId() is { } usuarioId)
                {
                    var sessao = await sessoes.ObterPorIdAsync(sessaoId, ct);
                    if (sessao is not null && sessao.UsuarioId == usuarioId)
                    {
                        sessao.Revogar(relogio.Agora);
                        await unidade.SalvarAsync(ct);
                    }
                }

                await http.SignOutAsync(IdentityConstants.ApplicationScheme);
                LimparCookie(http, opcoes);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .RequireRateLimiting("escrita");

        app.MapPost("/api/auth/sair-de-todos", async (
                HttpContext http,
                UserManager<ApplicationUser> usuarios,
                ISessaoRepository sessoes,
                IUnitOfWork unidade,
                IRelogio relogio,
                AutenticacaoOptions opcoes,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();

                // Invalidação primária: revoga todas as linhas de dispositivo.
                await sessoes.RevogarTodasAsync(usuarioId, relogio.Agora, exceto: null, ct);
                await unidade.SalvarAsync(ct);

                // Defesa em profundidade: cookies antigos deixam de ser aceitos.
                var conta = await usuarios.FindByIdAsync(usuarioId.ToString());
                if (conta is not null)
                {
                    await usuarios.UpdateSecurityStampAsync(conta);
                }

                await http.SignOutAsync(IdentityConstants.ApplicationScheme);
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
        app.MapPost("/api/conta/consentimento", async (
                RegistrarConsentimentoRequest entrada,
                HttpContext http,
                ConsentimentoService consentimento,
                LegalOptions opcoes,
                CancellationToken ct) =>
            {
                var usuarioId = http.ExigirUsuarioId();
                var termosVersao = (entrada.TermosVersao ?? string.Empty).Trim();
                var privacidadeVersao = (entrada.PrivacidadeVersao ?? string.Empty).Trim();

                if (termosVersao != opcoes.TermsVersion || privacidadeVersao != opcoes.PrivacyVersion)
                {
                    throw new DomainException(
                        "consentimento_versao_invalida",
                        "As versões enviadas não correspondem aos termos vigentes.");
                }

                await consentimento.RegistrarAsync(usuarioId, termosVersao, privacidadeVersao, ct);
                return Results.NoContent();
            })
            .RequireRateLimiting("escrita");

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

                // O cookie ativo é o da Identity; LimparCookie só remove o legado.
                await http.SignOutAsync(IdentityConstants.ApplicationScheme);
                LimparCookie(http, opcoes);
                return Results.NoContent();
            })
            .RequireRateLimiting("escrita");

        return app;
    }

    // Espelha a condição de registro do handler em IdentityExtensions: sem as duas
    // credenciais o Google permanece indisponível, mas o app sobe normalmente.
    public static bool GoogleConfigurado(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Google:ClientId"])
        && !string.IsNullOrWhiteSpace(configuration["Google:ClientSecret"]);

    // Apenas limpa o cookie obsoleto fisga_sessao no logout; nenhum cookie novo é gravado.
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

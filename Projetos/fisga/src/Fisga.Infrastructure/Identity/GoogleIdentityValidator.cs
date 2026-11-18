using System.Security.Claims;
using Fisga.Domain.Common;
using Fisga.Domain.Identidade;
using Microsoft.AspNetCore.Identity;

namespace Fisga.Infrastructure.Identity;

/// <summary>
/// Identidade já validada pelo handler do Google. Carrega apenas o subject, o
/// e-mail, o nome e o sinal de que o e-mail foi verificado — nenhum token ou
/// dado sensível é repassado adiante. O padrão <c>true</c> preserva os chamadores
/// que já recebem uma identidade validada pelo <see cref="GoogleIdentityValidator"/>.
/// </summary>
public sealed record GoogleUsuario(
    string Subject,
    Email Email,
    string? Nome,
    bool EmailVerified = false);

/// <summary>
/// Valida o <see cref="ExternalLoginInfo"/> produzido pelo handler OAuth do
/// Google. O handler já trocou o código pelo token e chamou o userinfo; aqui só
/// conferimos o contrato mínimo confiável: provedor, subject não vazio, e-mail
/// presente e verificado pelo Google. Não verificamos assinatura de token nem
/// adicionamos pacotes — isso é responsabilidade do <c>AddGoogle</c>.
/// </summary>
public sealed class GoogleIdentityValidator
{
    public const string Provider = "Google";

    // O claim action registrado em IdentityExtensions copia o campo booleano
    // "email_verified" do userinfo do Google para este tipo de claim.
    public const string EmailVerifiedClaim = "email_verified";

    public Task<GoogleUsuario> ValidarAsync(
        ExternalLoginInfo info,
        CancellationToken ct = default)
    {
        if (!string.Equals(info.LoginProvider, Provider, StringComparison.Ordinal))
        {
            throw new DomainException(
                "google_provider_invalido",
                "Este login não veio do Google.");
        }

        var subject = info.ProviderKey;
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new DomainException(
                "google_subject_invalido",
                "A identidade do Google não pôde ser confirmada.");
        }

        var emailBruto = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(emailBruto))
        {
            throw new DomainException(
                "google_email_ausente",
                "O Google não devolveu um e-mail para esta conta.");
        }

        var verificado = info.Principal.FindFirstValue(EmailVerifiedClaim);
        if (!bool.TryParse(verificado, out var emailVerificado) || !emailVerificado)
        {
            throw new DomainException(
                "google_email_nao_verificado",
                "O e-mail desta conta do Google não está verificado.");
        }

        var nome = info.Principal.FindFirstValue(ClaimTypes.Name);
        return Task.FromResult(new GoogleUsuario(
            subject.Trim(),
            Email.Criar(emailBruto),
            string.IsNullOrWhiteSpace(nome) ? null : nome.Trim(),
            emailVerificado));
    }
}

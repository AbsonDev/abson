using System.Security.Cryptography;
using Fisga.Domain.Common;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace Fisga.Infrastructure.Identity;

/// <summary>
/// Vincula explicitamente a conta Google à conta Fisga já autenticada em
/// <c>/conta</c>. Diferente do login (Task 8), aqui a identidade é decidida pela
/// sessão atual: um subject alheio ou um e-mail divergente são recusados, nunca
/// trocamos de conta. O único efeito de escrita é <c>AddLoginAsync</c> — sem
/// <c>SignInAsync</c>, sem nova sessão e sem novo cookie <c>fisga_auth</c>.
/// </summary>
public sealed class ConexaoGoogleService
{
    public const string IntencaoCookie = "fisga_google_intencao";
    public const string PropositoIntencao = "google-connect";
    public const int ValidadeIntencaoEmMinutos = 5;

    private readonly IDataProtector _protetor;
    private readonly UserManager<ApplicationUser> _usuarios;

    public ConexaoGoogleService(
        IDataProtectionProvider provedor,
        UserManager<ApplicationUser> usuarios)
    {
        _protetor = provedor.CreateProtector(PropositoIntencao);
        _usuarios = usuarios;
    }

    /// <summary>
    /// Cria o valor da intenção: <c>usuarioId|nonce|expiraEm</c> protegido pelo
    /// data protection. O nonce garante unicidade mesmo para a mesma conta; o
    /// prazo é verificado de novo no callback, independente do cookie do browser.
    /// </summary>
    public Task<string> CriarIntencaoAsync(Guid usuarioId)
    {
        var expiraEm = DateTimeOffset.UtcNow.AddMinutes(ValidadeIntencaoEmMinutos).UtcTicks;
        var valor = string.Join('|', usuarioId.ToString("N"), Guid.NewGuid().ToString("N"), expiraEm);
        return Task.FromResult(_protetor.Protect(valor));
    }

    /// <summary>
    /// Confere que a intenção íntegra pertence a este usuário e não expirou.
    /// Qualquer falha de descriptografia, formato ou prazo é recusada.
    /// </summary>
    public Task ValidarIntencaoAsync(string valor, Guid usuarioId)
    {
        string desprotegido;
        try
        {
            desprotegido = _protetor.Unprotect(valor);
        }
        catch (CryptographicException)
        {
            throw new DomainException(
                "google_intencao_invalida",
                "A solicitação de conexão é inválida. Comece novamente.");
        }

        var partes = desprotegido.Split('|');
        if (partes.Length != 3
            || !Guid.TryParseExact(partes[0], "N", out var dono)
            || dono != usuarioId
            || !Guid.TryParseExact(partes[1], "N", out _)
            || !long.TryParse(partes[2], out var expiraEm))
        {
            throw new DomainException(
                "google_intencao_invalida",
                "A solicitação de conexão é inválida. Comece novamente.");
        }

        if (DateTimeOffset.UtcNow.UtcTicks >= expiraEm)
        {
            throw new DomainException(
                "google_intencao_expirada",
                "A solicitação de conexão expirou. Comece novamente.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Vincula o subject do Google à conta atual. Exige e-mail verificado nos
    /// dois lados e e-mail idêntico (normalizado); nunca promove ou troca de conta.
    /// </summary>
    public async Task ConectarAsync(
        ApplicationUser conta,
        GoogleUsuario google,
        CancellationToken ct = default)
    {
        if (!google.EmailVerified)
        {
            throw new DomainException(
                "google_email_nao_verificado",
                "O e-mail desta conta do Google não está verificado.");
        }

        if (!conta.EmailConfirmed || string.IsNullOrWhiteSpace(conta.Email))
        {
            throw new DomainException(
                "google_email_nao_verificado_localmente",
                "Confirme seu e-mail no Fisga antes de conectar o Google.");
        }

        var emailLocal = conta.Email.Trim().ToLowerInvariant();
        if (!string.Equals(google.Email.Valor, emailLocal, StringComparison.Ordinal))
        {
            throw new DomainException(
                "google_email_divergente",
                "O e-mail do Google deve ser o mesmo e verificado da sua conta Fisga.");
        }

        var vinculada = await _usuarios.FindByLoginAsync(
            GoogleIdentityValidator.Provider, google.Subject);
        if (vinculada is not null && vinculada.Id != conta.Id)
        {
            // Subject pertence a outro usuário: recusar sem nunca trocar de conta.
            throw new DomainException(
                "google_vinculo_falhou",
                "Esta conta do Google já está associada a outro usuário.");
        }

        if (vinculada is not null)
        {
            // Já vinculado a esta conta: confirma sem duplicar a linha.
            return;
        }

        // Único efeito de escrita: sem SignInAsync, sem Sessao, sem cookie novo.
        var resultado = await _usuarios.AddLoginAsync(
            conta,
            new UserLoginInfo(
                GoogleIdentityValidator.Provider,
                google.Subject,
                GoogleIdentityValidator.Provider));
        if (!resultado.Succeeded)
        {
            throw new DomainException(
                "google_vinculo_falhou",
                "Esta conta do Google já está associada a outro usuário.");
        }
    }
}

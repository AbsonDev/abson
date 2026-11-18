using Fisga.Application.Identidade;
using Fisga.Domain.Common;
using Fisga.Domain.Identidade;
using Microsoft.AspNetCore.Identity;

namespace Fisga.Infrastructure.Identity;

/// <summary>
/// Resolve o login com Google para uma conta local. A ordem é security-critical:
/// um subject conhecido entra na mesma conta; um e-mail conhecido só é vinculado
/// quando a posse foi provada também no lado local; um e-mail novo é criado já
/// verificado. Nunca promovemos uma conta de senha não confirmada — desde a Task
/// 6, chamar <c>GarantirUsuarioAsync(emailVerificado: true)</c> antes da checagem
/// local permitiria tomada de conta.
/// </summary>
public sealed class GoogleIdentityService(
    UserManager<ApplicationUser> usuarios,
    IUsuarioRepository repositorio,
    ICadastroDeUsuario cadastro)
{
    public async Task<Usuario> EntrarOuCriarAsync(
        GoogleUsuario google,
        CancellationToken ct = default)
    {
        // (1) O subject já pertence a uma conta: entra nela, sem olhar o e-mail.
        var vinculada = await usuarios.FindByLoginAsync(
            GoogleIdentityValidator.Provider, google.Subject);
        if (vinculada is not null)
        {
            return await ObterUsuarioDaContaAsync(vinculada, ct);
        }

        // (2)/(3) E-mail já conhecido: exige verificação local nos DOIS lados ANTES
        // de qualquer escrita. Não chamar GarantirUsuarioAsync aqui — ela promoveria
        // uma conta de senha ainda não confirmada.
        var existente = await repositorio.ObterPorEmailAsync(google.Email, ct);
        if (existente is not null)
        {
            var conta = await usuarios.FindByIdAsync(existente.Id.ToString());
            if (existente.EmailVerificadoEm is null || conta is null || !conta.EmailConfirmed)
            {
                throw new DomainException(
                    "google_email_nao_verificado_localmente",
                    "Confirme seu e-mail no Fisga antes de entrar com o Google.");
            }

            await VincularAsync(conta, google.Subject);
            return existente;
        }

        // (4) E-mail novo: a verificação do Google é suficiente para criar a conta
        // já confirmada e, em seguida, registrar o login externo.
        var usuario = await cadastro.GarantirUsuarioAsync(
            google.Email, google.Nome, emailVerificado: true, ct);
        var contaNova = await usuarios.FindByIdAsync(usuario.Id.ToString())
            ?? throw new DomainException(
                "google_conta_inconsistente",
                "Não foi possível localizar a conta recém-criada.");

        await VincularAsync(contaNova, google.Subject);
        return usuario;
    }

    private async Task<Usuario> ObterUsuarioDaContaAsync(
        ApplicationUser conta,
        CancellationToken ct) =>
        await cadastro.ObterUsuarioAsync(conta.Id, ct)
        ?? throw new DomainException(
            "google_conta_inconsistente",
            "Não foi possível localizar o usuário desta conta.");

    private async Task VincularAsync(ApplicationUser conta, string subject)
    {
        IdentityResult resultado;
        try
        {
            resultado = await usuarios.AddLoginAsync(
                conta,
                new UserLoginInfo(
                    GoogleIdentityValidator.Provider,
                    subject,
                    GoogleIdentityValidator.Provider));
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            // Só traduzimos quando o subject PASSOU a existir na base — assinatura de
            // que outro request venceu a corrida. Falhas genuínas de infraestrutura
            // (conexão, disco, bug) não acham login e propagam para o middleware.
            var dono = await usuarios.FindByLoginAsync(GoogleIdentityValidator.Provider, subject);
            if (dono is null)
            {
                throw;
            }

            // Nunca trocamos de conta nem sobrescrevemos um vínculo alheio.
            throw new DomainException(
                "google_vinculo_falhou",
                "Esta conta do Google já está associada a outro usuário.");
        }

        if (!resultado.Succeeded)
        {
            throw new DomainException(
                "google_vinculo_falhou",
                "Esta conta do Google já está associada a outro usuário.");
        }
    }
}

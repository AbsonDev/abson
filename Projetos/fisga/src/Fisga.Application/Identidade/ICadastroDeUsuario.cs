using Fisga.Domain.Identidade;

namespace Fisga.Application.Identidade;

/// <summary>
/// Provisiona contas: garante a criação do <see cref="Usuario"/> de domínio e da
/// respectiva conta de autenticação da ASP.NET Core Identity com o mesmo Id,
/// de forma transacional.
/// </summary>
public interface ICadastroDeUsuario
{
    /// <summary>
    /// Garante o <see cref="Usuario"/> e a respectiva conta da Identity com o mesmo
    /// Id, criando ambos quando ainda não existem, em uma única transação.
    /// <paramref name="emailVerificado"/> marca <c>Usuario.EmailVerificadoEm</c> e
    /// <c>ApplicationUser.EmailConfirmed</c> — tanto na criação quanto quando o
    /// e-mail já pertence a uma conta existente ainda não confirmada (ex.: link
    /// mágico ou login externo do Google). Nunca rebaixa uma conta já confirmada.
    /// A conta é criada sem senha.
    /// </summary>
    Task<Usuario> GarantirUsuarioAsync(
        Email email,
        string? nome,
        bool emailVerificado,
        CancellationToken ct = default);

    /// <summary>
    /// Cria o <see cref="Usuario"/> de domínio e a conta de autenticação com senha
    /// com o mesmo Id, em uma única transação. A senha é validada entre 10 e 128
    /// caracteres (a Identity não impõe máximo nativo).
    /// </summary>
    Task<Usuario> CriarComSenhaAsync(
        string email,
        string? nome,
        string senha,
        CancellationToken ct = default);

    /// <summary>Obtém o usuário de domínio pelo Id, ou <c>null</c> quando não existe.</summary>
    Task<Usuario?> ObterUsuarioAsync(Guid id, CancellationToken ct = default);
}

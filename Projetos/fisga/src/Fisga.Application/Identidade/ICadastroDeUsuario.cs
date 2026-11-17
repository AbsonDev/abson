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
    /// Garante um <see cref="Usuario"/> para o e-mail, criando quando ainda não
    /// existe. <paramref name="emailVerificado"/> marca o e-mail como verificado
    /// no momento da criação (ex.: login externo do Google).
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

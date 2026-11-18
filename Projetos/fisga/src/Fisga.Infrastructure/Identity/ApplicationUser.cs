using Microsoft.AspNetCore.Identity;

namespace Fisga.Infrastructure.Identity;

/// <summary>
/// Conta de autenticação da ASP.NET Core Identity. Guarda apenas credenciais,
/// claims, logins externos, security stamp, lockout e tokens. O <see cref="Id"/>
/// espelha o <c>Id</c> do <c>Usuario</c> do domínio, que segue com nome,
/// preferências, verificação e os dados do produto.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
}

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Fisga.Application.Identidade;
using Fisga.Domain.Identidade;
using Fisga.Infrastructure.Persistence;

namespace Fisga.Infrastructure.Identity;

/// <summary>
/// Exclusão em duas camadas: primeiro a conta da Identity (cascateando logins,
/// tokens e claims), depois os tokens de link mágico/reset que não têm FK para
/// a conta. Idempotente para permitir que o suporte repita após falha parcial.
/// </summary>
public sealed class ExclusaoDeIdentidade(
    UserManager<ApplicationUser> usuarios,
    FisgaDbContext db) : IExclusaoDeIdentidade
{
    public async Task ExcluirAsync(Usuario usuario, CancellationToken ct = default)
    {
        var conta = await usuarios.FindByIdAsync(usuario.Id.ToString());
        if (conta is not null)
        {
            var resultado = await usuarios.DeleteAsync(conta);
            if (!resultado.Succeeded)
            {
                throw new InvalidOperationException(
                    "Não foi possível remover a credencial da conta: " +
                    string.Join("; ", resultado.Errors.Select(e => e.Description)));
            }
        }

        await db.TokensDeLogin
            .Where(token => token.Email == usuario.Email.Valor)
            .ExecuteDeleteAsync(ct);
    }
}

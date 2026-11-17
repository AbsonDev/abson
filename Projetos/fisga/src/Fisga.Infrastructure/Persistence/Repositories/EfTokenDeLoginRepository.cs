using Microsoft.EntityFrameworkCore;
using Fisga.Application.Identidade;
using Fisga.Domain.Identidade;

namespace Fisga.Infrastructure.Persistence.Repositories;

public sealed class EfTokenDeLoginRepository(FisgaDbContext db) : ITokenDeLoginRepository
{
    public async Task<TokenDeLogin?> ObterPorHashAsync(string tokenHash, CancellationToken ct = default) =>
        await db.TokensDeLogin.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, ct);

    public async Task<TokenDeLogin?> ObterUltimoPorEmailAsync(string email, CancellationToken ct = default)
    {
        // SQLite não ordena DateTimeOffset no servidor; ordenamos na aplicação.
        var tokens = await db.TokensDeLogin
            .Where(token => token.Email == email)
            .ToListAsync(ct);

        return tokens.OrderByDescending(token => token.CriadoEm).FirstOrDefault();
    }

    public async Task InvalidarPendentesAsync(
        string email,
        DateTimeOffset agora,
        CancellationToken ct = default)
    {
        var pendentes = await db.TokensDeLogin
            .Where(token => token.Email == email && token.UsadoEm == null)
            .ToListAsync(ct);

        foreach (var token in pendentes)
        {
            token.Invalidar(agora);
        }
    }

    public void Adicionar(TokenDeLogin token) => db.TokensDeLogin.Add(token);
}

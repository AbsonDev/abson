using Microsoft.EntityFrameworkCore;
using Fisga.Application.Identidade;
using Fisga.Domain.Identidade;

namespace Fisga.Infrastructure.Persistence.Repositories;

public sealed class EfUsuarioRepository(FisgaDbContext db) : IUsuarioRepository
{
    public async Task<Usuario?> ObterPorEmailAsync(Email email, CancellationToken ct = default) =>
        await db.Usuarios.FirstOrDefaultAsync(usuario => usuario.Email == email, ct);

    public async Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Usuarios.FirstOrDefaultAsync(usuario => usuario.Id == id, ct);

    public void Adicionar(Usuario usuario) => db.Usuarios.Add(usuario);

    public void Remover(Usuario usuario) => db.Usuarios.Remove(usuario);
}

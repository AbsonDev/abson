using Microsoft.EntityFrameworkCore;
using Fisga.Application.Identidade;
using Fisga.Domain.Identidade;

namespace Fisga.Infrastructure.Persistence.Repositories;

public sealed class EfConsentimentoRepository(FisgaDbContext db) : IConsentimentoRepository
{
    public async Task<Consentimento?> ObterUltimoAsync(
        Guid usuarioId,
        TipoDeConsentimento tipo,
        CancellationToken ct = default)
    {
        // SQLite não ordena DateTimeOffset no servidor; ordenamos na aplicação.
        var consentimentos = await db.Consentimentos
            .Where(consentimento => consentimento.UsuarioId == usuarioId && consentimento.Tipo == tipo)
            .ToListAsync(ct);

        return consentimentos
            .OrderByDescending(consentimento => consentimento.AceitoEm)
            .FirstOrDefault();
    }

    public async Task<bool> TemVersaoAsync(
        Guid usuarioId,
        TipoDeConsentimento tipo,
        string versao,
        CancellationToken ct = default) =>
        await db.Consentimentos
            .AnyAsync(consentimento => consentimento.UsuarioId == usuarioId
                                       && consentimento.Tipo == tipo
                                       && consentimento.Versao == versao,
                ct);

    public void Adicionar(Consentimento consentimento) => db.Consentimentos.Add(consentimento);
}

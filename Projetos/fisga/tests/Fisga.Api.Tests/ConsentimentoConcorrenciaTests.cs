using Microsoft.EntityFrameworkCore;
using Fisga.Application.Abstractions;
using Fisga.Application.Identidade;
using Fisga.Domain.Identidade;

namespace Fisga.Api.Tests;

public sealed class ConsentimentoConcorrenciaTests
{
    [Fact]
    public async Task Aceite_concorrente_com_violacao_unica_e_tratado_como_sucesso()
    {
        var servico = new ConsentimentoService(
            new RepositorioSemVersao(),
            new UnidadeQuePerdeACorrida(),
            new RelogioFixo(),
            new LegalOptions { TermsVersion = "2026-11-17", PrivacyVersion = "2026-11-17" });

        await servico.RegistrarAsync(Guid.NewGuid(), "2026-11-17", "2026-11-17");
    }

    private sealed class RepositorioSemVersao : IConsentimentoRepository
    {
        public Task<Consentimento?> ObterUltimoAsync(
            Guid usuarioId,
            TipoDeConsentimento tipo,
            CancellationToken ct = default) =>
            Task.FromResult<Consentimento?>(null);

        public Task<bool> TemVersaoAsync(
            Guid usuarioId,
            TipoDeConsentimento tipo,
            string versao,
            CancellationToken ct = default) =>
            Task.FromResult(false);

        public void Adicionar(Consentimento consentimento)
        {
        }
    }

    private sealed class UnidadeQuePerdeACorrida : IUnitOfWork
    {
        public Task<int> SalvarAsync(CancellationToken ct = default) =>
            throw new DbUpdateException(
                "Falha ao salvar",
                new Exception("duplicate key value violates unique constraint \"IX_consentimentos\""));
    }

    private sealed class RelogioFixo : IRelogio
    {
        public DateTimeOffset Agora => new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    }
}

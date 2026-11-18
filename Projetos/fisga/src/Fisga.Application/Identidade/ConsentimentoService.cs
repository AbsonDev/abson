using Fisga.Application.Abstractions;
using Fisga.Domain.Identidade;

namespace Fisga.Application.Identidade;

/// <summary>
/// Avalia e registra o aceite formal dos termos e da privacidade vigentes.
/// Não grava versões retroativas: um usuário só deixa de estar pendente quando
/// existe no banco um aceite para cada versão atualmente configurada.
/// </summary>
public sealed class ConsentimentoService(
    IConsentimentoRepository consentimentos,
    IUnitOfWork unidade,
    IRelogio relogio,
    LegalOptions opcoes)
{
    public async Task<bool> EstaPendenteAsync(Guid usuarioId, CancellationToken ct = default) =>
        !await consentimentos.TemVersaoAsync(usuarioId, TipoDeConsentimento.Termos, opcoes.TermsVersion, ct)
        || !await consentimentos.TemVersaoAsync(usuarioId, TipoDeConsentimento.Privacidade, opcoes.PrivacyVersion, ct);

    public async Task RegistrarAsync(
        Guid usuarioId,
        string termosVersao,
        string privacidadeVersao,
        CancellationToken ct = default)
    {
        var agora = relogio.Agora;

        // O índice único (usuario, tipo, versão) impede duplicatas: um segundo
        // aceite da mesma versão não deve virar um 500 para o usuário.
        if (!await consentimentos.TemVersaoAsync(usuarioId, TipoDeConsentimento.Termos, termosVersao, ct))
        {
            consentimentos.Adicionar(Consentimento.Criar(usuarioId, TipoDeConsentimento.Termos, termosVersao, agora));
        }

        if (!await consentimentos.TemVersaoAsync(usuarioId, TipoDeConsentimento.Privacidade, privacidadeVersao, ct))
        {
            consentimentos.Adicionar(Consentimento.Criar(usuarioId, TipoDeConsentimento.Privacidade, privacidadeVersao, agora));
        }

        try
        {
            await unidade.SalvarAsync(ct);
        }
        catch (Exception excecao) when (EhViolacaoUnica(excecao))
        {
            // Duas abas/requisições aceitaram a mesma versão ao mesmo tempo:
            // o índice único garantiu um só registro; o resultado desejado é 204.
        }
    }

    private static bool EhViolacaoUnica(Exception excecao)
    {
        if (excecao.GetType().FullName != "Microsoft.EntityFrameworkCore.DbUpdateException")
        {
            return false;
        }

        var texto = excecao.ToString();
        return texto.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
               || texto.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase);
    }
}

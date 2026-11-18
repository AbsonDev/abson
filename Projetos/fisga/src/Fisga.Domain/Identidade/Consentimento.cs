using Fisga.Domain.Common;

namespace Fisga.Domain.Identidade;

/// <summary>
/// Aceite de Termos ou Privacidade. Guarda a versão aceita e a data; o aceite é
/// exigido para a versão vigente de cada tipo e não é inventado retroativamente.
/// </summary>
public sealed class Consentimento
{
    private Consentimento()
    {
    }

    private Consentimento(
        Guid id,
        Guid usuarioId,
        TipoDeConsentimento tipo,
        string versao,
        DateTimeOffset aceitoEm)
    {
        Id = id;
        UsuarioId = usuarioId;
        Tipo = tipo;
        Versao = versao;
        AceitoEm = aceitoEm;
    }

    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    public TipoDeConsentimento Tipo { get; private set; }

    public string Versao { get; private set; } = string.Empty;

    public DateTimeOffset AceitoEm { get; private set; }

    public static Consentimento Criar(
        Guid usuarioId,
        TipoDeConsentimento tipo,
        string versao,
        DateTimeOffset aceitoEm)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new DomainException("consentimento_sem_usuario", "Consentimento sem usuário.");
        }

        if (!Enum.IsDefined(tipo))
        {
            throw new DomainException("consentimento_tipo_invalido", "Tipo de consentimento inválido.");
        }

        if (string.IsNullOrWhiteSpace(versao) || versao.Trim().Length > 64)
        {
            throw new DomainException("consentimento_versao_invalida", "Versão do consentimento inválida.");
        }

        return new Consentimento(Guid.NewGuid(), usuarioId, tipo, versao.Trim(), aceitoEm);
    }
}

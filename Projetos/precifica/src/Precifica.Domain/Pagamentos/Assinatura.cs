using Precifica.Domain.Common;
using Precifica.Domain.Pagamentos.Eventos;

namespace Precifica.Domain.Pagamentos;

public enum StatusDaAssinatura
{
    Pendente = 1,
    Ativa = 2,
    Inadimplente = 3,
    Cancelada = 4
}

/// <summary>
/// Assinatura mensal do plano Pro. O acesso sem marca d'água é verificado
/// pelo e-mail do assinante no momento do download.
/// </summary>
public sealed class Assinatura : AggregateRoot
{
    private Assinatura()
    {
    }

    private Assinatura(Guid id, string email, Dinheiro valorMensal, DateTimeOffset criadaEm)
    {
        Id = id;
        Email = email;
        ValorMensal = valorMensal;
        CriadaEm = criadaEm;
        Status = StatusDaAssinatura.Pendente;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public Dinheiro ValorMensal { get; private set; }

    public StatusDaAssinatura Status { get; private set; }

    public string? ReferenciaGateway { get; private set; }

    public DateTimeOffset CriadaEm { get; private set; }

    public DateTimeOffset? PeriodoAtualFim { get; private set; }

    public DateTimeOffset? CanceladaEm { get; private set; }

    public static Assinatura Criar(string email, Dinheiro valorMensal, DateTimeOffset agora)
    {
        if (valorMensal.Centavos <= 0)
        {
            throw new DomainException("assinatura_valor_invalido", "O valor da assinatura precisa ser maior que zero.");
        }

        return new Assinatura(Guid.NewGuid(), EmailDoCliente.Normalizar(email), valorMensal, agora);
    }

    public void VincularReferencia(string referencia)
    {
        if (string.IsNullOrWhiteSpace(referencia))
        {
            throw new DomainException("referencia_vazia", "Referência de assinatura inválida.");
        }

        ReferenciaGateway = referencia.Trim();
    }

    /// <summary>
    /// Ativa a assinatura por um mês. Chamadas repetidas do webhook não
    /// encurtam nem estendem um período já vigente.
    /// </summary>
    public void Ativar(string? referenciaGateway, DateTimeOffset agora)
    {
        if (Status == StatusDaAssinatura.Cancelada)
        {
            throw new DomainException("assinatura_cancelada", "Uma assinatura cancelada não pode ser reativada por este fluxo.");
        }

        if (!string.IsNullOrWhiteSpace(referenciaGateway))
        {
            ReferenciaGateway = referenciaGateway.Trim();
        }

        var jaEstavaAtiva = Status == StatusDaAssinatura.Ativa;
        var fimVigente = PeriodoAtualFim;

        Status = StatusDaAssinatura.Ativa;
        PeriodoAtualFim = fimVigente is not null && fimVigente > agora
            ? fimVigente
            : agora.AddMonths(1);

        if (!jaEstavaAtiva)
        {
            Registrar(new AssinaturaAtivada(Id, Email, PeriodoAtualFim.Value, agora));
        }
    }

    public void MarcarInadimplente()
    {
        if (Status == StatusDaAssinatura.Ativa)
        {
            Status = StatusDaAssinatura.Inadimplente;
        }
    }

    public void Cancelar(DateTimeOffset agora)
    {
        if (Status == StatusDaAssinatura.Cancelada)
        {
            return;
        }

        Status = StatusDaAssinatura.Cancelada;
        CanceladaEm = agora;
    }

    public bool EstaAtiva(DateTimeOffset agora) =>
        Status == StatusDaAssinatura.Ativa
        && (PeriodoAtualFim is null || PeriodoAtualFim > agora);
}

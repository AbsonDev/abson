using Precifica.Domain.Common;
using Precifica.Domain.Pagamentos.Eventos;

namespace Precifica.Domain.Pagamentos;

/// <summary>
/// Compra avulsa do PDF sem marca d'água. O acesso é vinculado ao cálculo,
/// então o pagamento vale para aquele link específico.
/// </summary>
public sealed class Pedido : AggregateRoot
{
    private Pedido()
    {
    }

    private Pedido(
        Guid id,
        Guid calculoId,
        string email,
        TipoDePedido tipo,
        Dinheiro valor,
        DateTimeOffset criadoEm)
    {
        Id = id;
        CalculoId = calculoId;
        Email = email;
        Tipo = tipo;
        Valor = valor;
        CriadoEm = criadoEm;
        Status = StatusDoPedido.Pendente;
    }

    public Guid Id { get; private set; }

    public Guid CalculoId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public TipoDePedido Tipo { get; private set; }

    public Dinheiro Valor { get; private set; }

    public StatusDoPedido Status { get; private set; }

    public string? ReferenciaGateway { get; private set; }

    public string? PagamentoGatewayId { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset? PagoEm { get; private set; }

    public static Pedido CriarPdfAvulso(
        Guid calculoId,
        string email,
        Dinheiro valor,
        DateTimeOffset agora)
    {
        if (calculoId == Guid.Empty)
        {
            throw new DomainException("pedido_sem_calculo", "O pedido precisa apontar para um cálculo.");
        }

        if (valor.Centavos <= 0)
        {
            throw new DomainException("pedido_valor_invalido", "O valor do pedido precisa ser maior que zero.");
        }

        return new Pedido(
            Guid.NewGuid(),
            calculoId,
            EmailDoCliente.Normalizar(email),
            TipoDePedido.PdfAvulso,
            valor,
            agora);
    }

    public void VincularReferencia(string referencia)
    {
        if (Status != StatusDoPedido.Pendente)
        {
            throw new DomainException("pedido_nao_pendente", "Só é possível vincular uma referência a um pedido pendente.");
        }

        if (string.IsNullOrWhiteSpace(referencia))
        {
            throw new DomainException("referencia_vazia", "Referência de pagamento inválida.");
        }

        ReferenciaGateway = referencia.Trim();
    }

    /// <summary>
    /// Idempotente: notificações repetidas do gateway não geram novo evento.
    /// </summary>
    public bool ConfirmarPagamento(string pagamentoGatewayId, Dinheiro valorPago, DateTimeOffset agora)
    {
        if (Status == StatusDoPedido.Pago)
        {
            return false;
        }

        if (Status != StatusDoPedido.Pendente)
        {
            throw new DomainException("pedido_nao_pendente", "Este pedido não está mais aguardando pagamento.");
        }

        if (string.IsNullOrWhiteSpace(pagamentoGatewayId))
        {
            throw new DomainException("pagamento_sem_id", "Pagamento sem identificador no gateway.");
        }

        if (valorPago < Valor)
        {
            throw new DomainException("pagamento_valor_divergente", "O valor pago é menor que o valor do pedido.");
        }

        Status = StatusDoPedido.Pago;
        PagamentoGatewayId = pagamentoGatewayId.Trim();
        PagoEm = agora;

        Registrar(new PagamentoConfirmado(Id, CalculoId, Valor, agora));
        return true;
    }

    public void Expirar()
    {
        if (Status != StatusDoPedido.Pendente)
        {
            return;
        }

        Status = StatusDoPedido.Expirado;
    }
}

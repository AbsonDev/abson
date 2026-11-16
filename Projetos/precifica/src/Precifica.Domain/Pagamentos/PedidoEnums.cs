namespace Precifica.Domain.Pagamentos;

public enum TipoDePedido
{
    PdfAvulso = 1
}

public enum StatusDoPedido
{
    Pendente = 1,
    Pago = 2,
    Expirado = 3,
    Cancelado = 4
}

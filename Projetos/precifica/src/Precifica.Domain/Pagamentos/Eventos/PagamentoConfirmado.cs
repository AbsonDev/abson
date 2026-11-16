using Precifica.Domain.Common;

namespace Precifica.Domain.Pagamentos.Eventos;

public sealed record PagamentoConfirmado(
    Guid PedidoId,
    Guid CalculoId,
    Dinheiro Valor,
    DateTimeOffset OcorridoEm) : IDomainEvent;

public sealed record AssinaturaAtivada(
    Guid AssinaturaId,
    string Email,
    DateTimeOffset PeriodoAtualFim,
    DateTimeOffset OcorridoEm) : IDomainEvent;

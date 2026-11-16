namespace Precifica.Application.Pagamentos;

/// <summary>
/// Chave de produto para o período de validação. Com <see cref="Ativa"/> desligada,
/// todos os cálculos liberam PDF sem marca d'água e os checkouts ficam bloqueados.
/// </summary>
public sealed class MonetizacaoOptions
{
    public bool Ativa { get; set; }
}

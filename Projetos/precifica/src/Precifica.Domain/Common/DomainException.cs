namespace Precifica.Domain.Common;

/// <summary>
/// Violação de uma regra de negócio. O código é estável e pode ser usado pela API/UI;
/// a mensagem é escrita para ser exibida ao usuário final.
/// </summary>
public sealed class DomainException(string codigo, string mensagem) : Exception(mensagem)
{
    public string Codigo { get; } = codigo;
}

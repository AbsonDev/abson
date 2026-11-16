namespace Precifica.Domain.Precificacao;

/// <summary>
/// Nichos atendidos no MVP. Cada um tem um modelo de custos próprio no
/// contexto de Catálogo (supporting), mas o cálculo é o mesmo.
/// </summary>
public enum Nicho
{
    Confeitaria = 1,
    Artesanato = 2,
    Impressao3D = 3,
    Estetica = 4
}

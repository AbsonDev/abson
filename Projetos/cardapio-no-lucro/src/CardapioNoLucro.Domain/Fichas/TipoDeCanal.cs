namespace CardapioNoLucro.Domain.Fichas;

/// <summary>
/// Canais de venda do food service. A comissão e a taxa fixa mudam o preço
/// necessário para o mesmo prato continuar lucrativo.
/// </summary>
public enum TipoDeCanal
{
    Salao = 1,
    Balcao = 2,
    DeliveryProprio = 3,
    IFood = 4,
    NoventaENoveFood = 5
}

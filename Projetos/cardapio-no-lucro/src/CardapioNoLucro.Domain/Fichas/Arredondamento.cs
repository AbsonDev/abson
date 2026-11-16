using CardapioNoLucro.Domain.Common;

namespace CardapioNoLucro.Domain.Fichas;

public enum Arredondamento
{
    Nenhum = 0,
    CinquentaCentavos = 1,
    Real = 2,
    CincoReais = 3
}

public static class ArredondamentoExtensions
{
    /// <summary>
    /// Arredonda sempre para cima, nunca para baixo: o preço sugerido não pode
    /// ficar abaixo da margem desejada por causa de centavos.
    /// </summary>
    public static Dinheiro ArredondarParaCima(this Arredondamento arredondamento, Dinheiro valor)
    {
        var passo = arredondamento switch
        {
            Arredondamento.Nenhum => 1,
            Arredondamento.CinquentaCentavos => 50,
            Arredondamento.Real => 100,
            Arredondamento.CincoReais => 500,
            _ => throw new DomainException("arredondamento_invalido", "Forma de arredondamento inválida.")
        };

        if (valor.Centavos <= 0)
        {
            return valor;
        }

        var resto = valor.Centavos % passo;
        return resto == 0 ? valor : new Dinheiro(valor.Centavos + (passo - resto));
    }
}

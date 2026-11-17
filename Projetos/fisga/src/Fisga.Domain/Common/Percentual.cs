namespace Fisga.Domain.Common;

/// <summary>
/// Percentual em pontos-base (1% = 100 pontos-base). Valores como 4,99% são
/// representados exatamente, sem depender de arredondamento de ponto flutuante.
/// </summary>
public readonly record struct Percentual : IComparable<Percentual>
{
    public Percentual(int pontosBase)
    {
        PontosBase = pontosBase;
    }

    public int PontosBase { get; }

    /// <summary>Valor percentual, por exemplo 4,99.</summary>
    public decimal Valor => PontosBase / 100m;

    /// <summary>Fração para multiplicação, por exemplo 0,0499.</summary>
    public decimal Fracao => PontosBase / 10_000m;

    public static Percentual Zero => new(0);

    public static Percentual De(decimal valorPercentual)
    {
        var pontos = decimal.Round(valorPercentual * 100m, 0, MidpointRounding.AwayFromZero);

        // Limite defensivo: evita overflow em entradas malformadas da API.
        if (pontos > int.MaxValue || pontos < int.MinValue)
        {
            throw new DomainException("percentual_fora_do_limite", "O percentual informado é grande demais.");
        }

        return new Percentual((int)pontos);
    }

    public Dinheiro AplicarSobre(Dinheiro valor) => valor.Multiplicar(Fracao);

    public static Percentual operator +(Percentual a, Percentual b) => new(a.PontosBase + b.PontosBase);

    public int CompareTo(Percentual outro) => PontosBase.CompareTo(outro.PontosBase);

    public static bool operator >(Percentual a, Percentual b) => a.PontosBase > b.PontosBase;

    public static bool operator <(Percentual a, Percentual b) => a.PontosBase < b.PontosBase;

    public static bool operator >=(Percentual a, Percentual b) => a.PontosBase >= b.PontosBase;

    public static bool operator <=(Percentual a, Percentual b) => a.PontosBase <= b.PontosBase;

    public override string ToString() => $"{Valor:0.##}%";
}

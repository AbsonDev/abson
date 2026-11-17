using System.Globalization;

namespace Fisga.Domain.Common;

/// <summary>
/// Dinheiro em centavos. Evita arredondamentos acumulados de <see cref="decimal"/>
/// e mantém a aritmética explícita no domínio.
/// </summary>
public readonly record struct Dinheiro : IComparable<Dinheiro>
{
    private static readonly CultureInfo CulturaBr = CultureInfo.GetCultureInfo("pt-BR");

    public Dinheiro(long centavos)
    {
        Centavos = centavos;
    }

    public long Centavos { get; }

    public decimal Reais => Centavos / 100m;

    public bool EhZero => Centavos == 0;

    public bool EhNegativo => Centavos < 0;

    public static Dinheiro Zero => new(0);

    public static Dinheiro DeReais(decimal reais)
    {
        // Limite defensivo: evita overflow em entradas malformadas da API.
        if (reais > 90_000_000_000_000_000m || reais < -90_000_000_000_000_000m)
        {
            throw new DomainException("valor_fora_do_limite", "O valor informado é grande demais.");
        }

        return new Dinheiro(Arredondar(reais * 100m));
    }

    public Dinheiro Somar(Dinheiro outro) => new(Centavos + outro.Centavos);

    public Dinheiro Subtrair(Dinheiro outro) => new(Centavos - outro.Centavos);

    public Dinheiro Multiplicar(decimal fator) => new(Arredondar(Centavos * fator));

    public Dinheiro Dividir(int divisor)
    {
        if (divisor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(divisor), "O divisor precisa ser maior que zero.");
        }

        return new Dinheiro(Arredondar((decimal)Centavos / divisor));
    }

    public static Dinheiro operator +(Dinheiro a, Dinheiro b) => a.Somar(b);

    public static Dinheiro operator -(Dinheiro a, Dinheiro b) => a.Subtrair(b);

    public static Dinheiro operator *(Dinheiro a, decimal fator) => a.Multiplicar(fator);

    public int CompareTo(Dinheiro outro) => Centavos.CompareTo(outro.Centavos);

    public static bool operator >(Dinheiro a, Dinheiro b) => a.Centavos > b.Centavos;

    public static bool operator <(Dinheiro a, Dinheiro b) => a.Centavos < b.Centavos;

    public static bool operator >=(Dinheiro a, Dinheiro b) => a.Centavos >= b.Centavos;

    public static bool operator <=(Dinheiro a, Dinheiro b) => a.Centavos <= b.Centavos;

    public override string ToString() => Reais.ToString("C", CulturaBr);

    private static long Arredondar(decimal valor) =>
        (long)decimal.Round(valor, 0, MidpointRounding.AwayFromZero);
}

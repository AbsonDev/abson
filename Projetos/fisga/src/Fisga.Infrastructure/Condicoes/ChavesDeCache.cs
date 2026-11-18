using System.Globalization;
using System.Text;

namespace Fisga.Infrastructure.Condicoes;

public static class ChavesDeCache
{
    private const int LimiteDoTermo = 80;

    public static string Condicoes(double lat, double lon, DateOnly dia) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"condicoes:v1:{lat:F2}:{lon:F2}:{dia:yyyy-MM-dd}");

    public static string Lua(double lat, double lon, DateOnly dia) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"lua:v1:{lat:F2}:{lon:F2}:{dia:yyyy-MM-dd}");

    public static string BuscaDeLocal(string termo) =>
        BuscaDeLocalNormalizada(NormalizarTermo(termo));

    public static string BuscaDeLocalNormalizada(string termoNormalizado) =>
        $"geocode:search:v1:{termoNormalizado}";

    public static string Reverso(double lat, double lon) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"geocode:reverse:v1:{lat:F3}:{lon:F3}");

    public static string NormalizarTermo(string termo)
    {
        if (string.IsNullOrWhiteSpace(termo))
        {
            return string.Empty;
        }

        var semAcento = new StringBuilder(termo.Length);
        foreach (var caractere in termo.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
            {
                semAcento.Append(caractere);
            }
        }

        var normalizado = string.Join(
            ' ',
            semAcento.ToString()
                .Normalize(NormalizationForm.FormC)
                .ToLowerInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (normalizado.Length <= LimiteDoTermo)
        {
            return normalizado;
        }

        // Não cortar um par substituto no meio: o emoji é um caractere lógico e
        // uma metade órfã gera chave inválida para logs/Sentry.
        var corte = char.IsHighSurrogate(normalizado[LimiteDoTermo - 1])
            ? LimiteDoTermo - 1
            : LimiteDoTermo;

        return normalizado[..corte];
    }
}

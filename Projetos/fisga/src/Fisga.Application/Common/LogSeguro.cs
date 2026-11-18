using System.Text;

namespace Fisga.Application.Common;

/// <summary>
/// Sanitiza valores de origem externa antes de escrever em log, evitando
/// injeção de quebra de linha (log forging) e Excessão de log por tamanho.
/// </summary>
public static class LogSeguro
{
    private const int LimitePadrao = 200;

    public static string ParaLog(string? valor, int limite = LimitePadrao)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return "-";
        }

        var limiteEfetivo = Math.Max(1, limite);
        var normalizado = valor.ReplaceLineEndings(" ");
        var construtor = new StringBuilder(Math.Min(normalizado.Length, limiteEfetivo));
        foreach (var caractere in normalizado)
        {
            if (!char.IsControl(caractere))
            {
                construtor.Append(caractere);
            }

            if (construtor.Length >= limiteEfetivo)
            {
                break;
            }
        }

        return construtor.ToString().Trim();
    }
}

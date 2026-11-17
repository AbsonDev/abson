using System.Security.Cryptography;
using System.Text;

namespace Fisga.Application.Newsletter;

/// <summary>
/// Token de descadastro assinado com HMAC. Não precisa de tabela extra: o
/// usuário é identificado pelo payload e a assinatura impede que alguém
/// descadastre outra pessoa.
/// </summary>
public static class AssinaturaDeDescadastro
{
    public static string Gerar(Guid usuarioId, string segredo)
    {
        var payload = usuarioId.ToString("N");
        return $"{payload}.{Assinar(payload, segredo)}";
    }

    public static Guid? Validar(string? token, string segredo)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var partes = token.Trim().Split('.');
        if (partes.Length != 2 || !Guid.TryParseExact(partes[0], "N", out var usuarioId))
        {
            return null;
        }

        var esperado = Encoding.UTF8.GetBytes(Assinar(partes[0], segredo));
        var recebido = Encoding.UTF8.GetBytes(partes[1]);

        if (esperado.Length != recebido.Length
            || !CryptographicOperations.FixedTimeEquals(esperado, recebido))
        {
            return null;
        }

        return usuarioId;
    }

    private static string Assinar(string payload, string segredo)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(segredo));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}

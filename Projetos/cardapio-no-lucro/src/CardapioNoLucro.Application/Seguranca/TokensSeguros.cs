using System.Security.Cryptography;
using System.Text;

namespace CardapioNoLucro.Application.Seguranca;

/// <summary>
/// Tokens opacos para link público e edição. O token de edição só é devolvido
/// uma vez; o banco guarda apenas o hash SHA-256.
/// </summary>
public static class TokensSeguros
{
    public static string Novo(int bytes = 16) =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes)).ToLowerInvariant();

    public static string Hash(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool Correspondem(string token, string hashEsperado)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(hashEsperado))
        {
            return false;
        }

        var hashRecebido = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        byte[] hashEsperadoBytes;
        try
        {
            hashEsperadoBytes = Convert.FromHexString(hashEsperado);
        }
        catch (FormatException)
        {
            return false;
        }

        return hashEsperadoBytes.Length == hashRecebido.Length
               && CryptographicOperations.FixedTimeEquals(hashRecebido, hashEsperadoBytes);
    }
}

using System.Net.Mail;
using Precifica.Domain.Common;

namespace Precifica.Domain.Common;

internal static class EmailDoCliente
{
    public static string Normalizar(string email)
    {
        var normalizado = (email ?? string.Empty).Trim().ToLowerInvariant();

        if (normalizado.Length > 254 || !MailAddress.TryCreate(normalizado, out _))
        {
            throw new DomainException("email_invalido", "Informe um e-mail válido para receber o acesso.");
        }

        return normalizado;
    }
}

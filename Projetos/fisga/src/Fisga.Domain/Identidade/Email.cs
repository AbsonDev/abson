using System.Text.RegularExpressions;
using Fisga.Domain.Common;

namespace Fisga.Domain.Identidade;

/// <summary>
/// E-mail normalizado do usuário. Guardamos sempre minúsculo para que
/// "Abson@..." e "abson@..." sejam a mesma conta.
/// </summary>
public sealed partial record Email
{
    private Email(string valor) => Valor = valor;

    public string Valor { get; }

    public string Dominio => Valor[(Valor.IndexOf('@') + 1)..];

    public static Email Criar(string? entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada))
        {
            throw new DomainException("email_obrigatorio", "Informe seu e-mail para entrar.");
        }

        var normalizado = entrada.Trim().ToLowerInvariant();

        if (normalizado.Length > 254 || !Formato().IsMatch(normalizado))
        {
            throw new DomainException("email_invalido", "Informe um e-mail válido.");
        }

        if (normalizado.Contains("..", StringComparison.Ordinal)
            || normalizado.StartsWith('.')
            || normalizado.Contains(".@", StringComparison.Ordinal))
        {
            throw new DomainException("email_invalido", "Informe um e-mail válido.");
        }

        return new Email(normalizado);
    }

    public override string ToString() => Valor;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex Formato();
}

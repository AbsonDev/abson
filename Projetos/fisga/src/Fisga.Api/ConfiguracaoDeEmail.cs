namespace Fisga.Api;

/// <summary>
/// Guardas de configuração que precisam falhar no start, antes de a aplicação
/// aceitar requisições. Produção exige Resend com chave; Console/Outbox são
/// exclusivos de Development (o outbox grava links com token em disco).
/// </summary>
public static class ConfiguracaoDeEmail
{
    public static void Validar(string? provedor, string? resendApiKey, bool desenvolvimento)
    {
        if (desenvolvimento)
        {
            return;
        }

        if (string.Equals(provedor, "Outbox", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Email:Provider=Outbox é exclusivo de Development: ele grava tokens em arquivo. " +
                "Configure Email:Provider=Resend em produção.");
        }

        if (!string.Equals(provedor, "Resend", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Em produção Email:Provider deve ser Resend. Console/Outbox são exclusivos de " +
                "Development e não enviam e-mail real.");
        }

        if (string.IsNullOrWhiteSpace(resendApiKey))
        {
            throw new InvalidOperationException(
                "Em produção Email:ResendApiKey não pode ser vazio: sem chave o envio falha aberto.");
        }
    }
}

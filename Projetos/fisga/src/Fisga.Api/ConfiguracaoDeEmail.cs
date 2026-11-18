namespace Fisga.Api;

/// <summary>
/// Guardas de configuração que precisam falhar no start, antes de a aplicação
/// aceitar requisições. O outbox de e-mail grava links com token em disco e só
/// pode existir em Development; em produção o envio é sempre Resend.
/// </summary>
public static class ConfiguracaoDeEmail
{
    public static void Validar(string? provedor, bool desenvolvimento)
    {
        if (!string.Equals(provedor, "Outbox", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!desenvolvimento)
        {
            throw new InvalidOperationException(
                "Email:Provider=Outbox é exclusivo de Development: ele grava tokens em arquivo. " +
                "Configure Email:Provider=Resend (ou remova a variável) em produção.");
        }
    }
}

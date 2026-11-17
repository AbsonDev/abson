using Microsoft.Extensions.Logging;
using Fisga.Infrastructure.Emails;

namespace Fisga.Api.Tests;

public sealed class ConsoleEmailSenderTests
{
    [Fact]
    public async Task EnviarLinkDeAcessoAsync_nao_loga_url_token_nem_destinatario()
    {
        var logger = new CapturingLogger();
        var sender = new ConsoleEmailSender(logger);
        var token = "TOKEN-SECRETO-ABC";
        var url = $"http://localhost:5184/api/auth/confirmar?token={token}";

        await sender.EnviarLinkDeAcessoAsync(
            "pescador@exemplo.com",
            "Pescador",
            url,
            DateTimeOffset.UtcNow.AddMinutes(15));

        var log = string.Join('\n', logger.Mensagens);

        Assert.DoesNotContain(token, log);
        Assert.DoesNotContain(url, log);
        Assert.DoesNotContain("pescador@exemplo.com", log);
        Assert.DoesNotContain("Pescador", log);
        Assert.Contains("exemplo.com", log);
    }

    [Fact]
    public async Task EnviarAsync_nao_loga_token_uid_url_nem_corpo()
    {
        var logger = new CapturingLogger();
        var sender = new ConsoleEmailSender(logger);
        var token = "TOKEN-CONFIRMACAO-123";
        var uid = Guid.NewGuid().ToString("N");
        var link = $"http://localhost:4323/confirmar-email?uid={uid}&token={token}";
        var texto = $"Confirme seu e-mail acessando: {link}";
        var html = $"<p><a href=\"{link}\">{link}</a></p>";

        await sender.EnviarAsync(
            "pescador@exemplo.com",
            "Confirme seu e-mail no Fisga",
            html,
            texto,
            $"http://localhost:4323/descadastro?token={token}");

        var log = string.Join('\n', logger.Mensagens);

        Assert.DoesNotContain(token, log);
        Assert.DoesNotContain(uid, log);
        Assert.DoesNotContain(link, log);
        Assert.DoesNotContain("Confirme seu e-mail acessando", log);
        Assert.DoesNotContain("pescador@exemplo.com", log);
        Assert.DoesNotContain("descadastro", log);
        Assert.Contains("exemplo.com", log);
        Assert.Contains("Confirme seu e-mail no Fisga", log);
    }

    private sealed class CapturingLogger : ILogger<ConsoleEmailSender>
    {
        public List<string> Mensagens { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Mensagens.Add(formatter(state, exception));
    }
}

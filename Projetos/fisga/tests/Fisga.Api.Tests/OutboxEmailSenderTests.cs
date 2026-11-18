using System.Text.Json;
using Fisga.Application.Identidade;
using Fisga.Infrastructure.Emails;

namespace Fisga.Api.Tests;

public sealed class OutboxEmailSenderTests : IDisposable
{
    private readonly string _pasta = Path.Combine(
        Path.GetTempPath(),
        $"fisga-outbox-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task EnviarAsync_grava_json_com_destino_e_link_sem_logar()
    {
        var sender = new OutboxEmailSender(new EmailOptions
        {
            Provider = "Outbox",
            OutboxPath = _pasta
        });

        var link = "http://localhost:4323/confirmar-email?uid=abc&token=TOKEN";
        await sender.EnviarAsync(
            "pescador@exemplo.com",
            "Confirme seu e-mail no Fisga",
            $"<p><a href=\"{link}\">{link}</a></p>",
            $"Confirme seu e-mail acessando: {link}");

        var arquivos = Directory.GetFiles(_pasta, "*.json");
        Assert.Single(arquivos);

        var registro = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            await File.ReadAllTextAsync(arquivos[0]));

        Assert.Equal("pescador@exemplo.com", registro!["destino"].GetString());
        Assert.Contains(link, registro["texto"].GetString());
        Assert.Contains(link, registro["html"].GetString());
    }

    [Fact]
    public async Task EnviarAsync_sem_outbox_configurado_falha_alto()
    {
        var sender = new OutboxEmailSender(new EmailOptions { Provider = "Outbox" });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.EnviarAsync("pescador@exemplo.com", "Assunto", "<p>html</p>", "texto"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_pasta))
        {
            Directory.Delete(_pasta, recursive: true);
        }
    }
}

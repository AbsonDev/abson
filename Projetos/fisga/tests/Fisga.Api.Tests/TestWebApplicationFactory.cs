using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Fisga.Application.Identidade;

namespace Fisga.Api.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"fisga-api-tests-{Guid.NewGuid():N}.db");

    public TestWebApplicationFactory()
    {
        // A API lê Database:* e Sentry durante Program.Main (no CreateBuilder),
        // antes de o ConfigureAppConfiguration ser aplicado ao host real. Por isso
        // usamos variáveis de ambiente, que entram na configuração do próprio
        // CreateBuilder e têm precedência sobre o appsettings*.json.
        Environment.SetEnvironmentVariable("Database__Provider", "Sqlite");
        Environment.SetEnvironmentVariable("Database__ConnectionString", $"Data Source={_dbPath}");
        Environment.SetEnvironmentVariable("Sentry__Dsn", "");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddScoped<IEmailSender, EmailSenderFake>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}

internal sealed class EmailSenderFake : IEmailSender
{
    public Task EnviarLinkDeAcessoAsync(
        string destino,
        string? nome,
        string url,
        DateTimeOffset expiraEm,
        CancellationToken ct = default) => Task.CompletedTask;

    public Task EnviarAsync(
        string destino,
        string assunto,
        string html,
        string texto,
        string? listUnsubscribeUrl = null,
        CancellationToken ct = default) => Task.CompletedTask;
}

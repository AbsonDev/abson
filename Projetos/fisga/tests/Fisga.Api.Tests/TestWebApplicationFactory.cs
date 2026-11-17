using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Fisga.Application.Identidade;
using Fisga.Infrastructure.Persistence;

namespace Fisga.Api.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;

    public TestWebApplicationFactory(string dbPath)
    {
        _dbPath = dbPath;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Aponta o DbContext para o SQLite temporário do fixture, independentemente
            // do que o Program.Main leu na configuração durante o CreateBuilder.
            services.RemoveAll<DbContextOptions<FisgaDbContext>>();
            services.RemoveAll<FisgaDbContext>();
            services.AddDbContext<FisgaDbContext>(options => options.UseSqlite($"Data Source={_dbPath}"));

            services.RemoveAll<IEmailSender>();
            services.AddScoped<IEmailSender, EmailSenderFake>();
        });
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

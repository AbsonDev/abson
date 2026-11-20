using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using Fisga.Api.RateLimiting;
using Fisga.Application.Identidade;
using Fisga.Infrastructure.Persistence;

namespace Fisga.Api.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;
    private readonly bool _neutralizarRateLimit;

    public TestWebApplicationFactory(string dbPath, bool neutralizarRateLimit = true)
    {
        _dbPath = dbPath;
        _neutralizarRateLimit = neutralizarRateLimit;
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

            // Neutraliza o rate limiter nos testes. As políticas por IP/1min ("auth",
            // "leitura", etc.) usam RemoteIpAddress, que é nulo no host em memória →
            // chave única "anonimo" compartilhada entre todas as classes; isso rejeitaria
            // requisições legítimas a partir da 9ª chamada na mesma janela. Reconfiguramos
            // os mesmos nomes como "sem limite" para não quebrar os endpoints. Com
            // neutralizarRateLimit: false a configuração real do Program prevalece, que é
            // o que prova o comportamento do pipeline de produção.
            if (_neutralizarRateLimit)
            {
                services.RemoveAll<IConfigureOptions<RateLimiterOptions>>();
                services.Configure<RateLimiterOptions>(options =>
                {
                    RejeicaoDoRateLimiter.Configurar(options);
                    options.AddPolicy<string>(PoliticasDeTaxa.Leitura, _ => RateLimitPartition.GetNoLimiter(string.Empty));
                    options.AddPolicy<string>(PoliticasDeTaxa.Escrita, _ => RateLimitPartition.GetNoLimiter(string.Empty));
                    options.AddPolicy<string>(PoliticasDeTaxa.Auth, _ => RateLimitPartition.GetNoLimiter(string.Empty));
                    options.AddPolicy<string>(PoliticasDeTaxa.Pdf, _ => RateLimitPartition.GetNoLimiter(string.Empty));
                    options.AddPolicy<string>(PoliticasDeTaxa.Midia, _ => RateLimitPartition.GetNoLimiter(string.Empty));
                });
            }
        });
    }
}

internal sealed class EmailSenderFake : IEmailSender
{
    private static readonly System.Collections.Concurrent.ConcurrentQueue<EmailRegistrado> EnviadosQueue = new();

    public static IReadOnlyList<EmailRegistrado> Enviados => EnviadosQueue.ToArray();

    public static void Limpar() => EnviadosQueue.Clear();

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
        CancellationToken ct = default)
    {
        EnviadosQueue.Enqueue(new EmailRegistrado(destino, assunto, html, texto));
        return Task.CompletedTask;
    }
}

internal sealed record EmailRegistrado(string Destino, string Assunto, string Html, string Texto);

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Fisga.Api;
using Fisga.Api.Endpoints;
using Fisga.Api.Middleware;
using Fisga.Api.Observabilidade;
using Fisga.Application;
using Fisga.Application.Identidade;
using Fisga.Infrastructure;
using Fisga.Infrastructure.Identity;
using Fisga.Infrastructure.Midia;
using Fisga.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Sentry;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseSentry(opcoes =>
{
    opcoes.SendDefaultPii = false;
    opcoes.MaxRequestBodySize = Sentry.Extensibility.RequestSize.None;
    opcoes.EnableLogs = true;
    opcoes.MinimumEventLevel = LogLevel.Warning;
    opcoes.MinimumBreadcrumbLevel = LogLevel.Information;
    opcoes.MaxBreadcrumbs = 50;
    opcoes.TracesSampleRate = TaxaDeAmostragem(builder.Environment);
    opcoes.SetBeforeSend(SentryScrubber.ScrubEvent);
    opcoes.SetBeforeSendTransaction(SentryScrubber.ScrubTransaction);
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(opcoes =>
{
    opcoes.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    opcoes.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.Configure<ForwardedHeadersOptions>(opcoes =>
{
    opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    opcoes.KnownIPNetworks.Clear();
    opcoes.KnownProxies.Clear();
});

builder.Services.AdicionarAplicacao();
builder.Services.AdicionarInfraestrutura(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddScoped<IdentitySessionService>();
builder.Services.AddScoped<ConexaoGoogleService>();

// Versões vigentes vêm da configuração (Legal); o aceite do usuário é lido do
// banco a cada requisição, sem claims de versão no cookie e sem reemissão de cookie.
builder.Services.Configure<LegalOptions>(builder.Configuration.GetSection("Legal"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<LegalOptions>>().Value);

var origens = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(opcoes =>
{
    opcoes.AddPolicy("web", politica =>
    {
        if (origens.Length == 0)
        {
            throw new InvalidOperationException("Configure Cors:AllowedOrigins com as origens do site.");
        }

        politica.WithOrigins(origens)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddRateLimiter(opcoes =>
{
    opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Limites por minuto/IP. Os valores padrão são os de produção; ambientes de
    // teste podem elevá-los por configuração (RateLimiting__Auth=1000) sem
    // alterar o comportamento padrão.
    AdicionarPolitica(opcoes, builder.Configuration, "leitura", 180);
    AdicionarPolitica(opcoes, builder.Configuration, "escrita", 40);
    AdicionarPolitica(opcoes, builder.Configuration, "auth", 8);
    AdicionarPolitica(opcoes, builder.Configuration, "pdf", 30);
    AdicionarPolitica(opcoes, builder.Configuration, "midia", 30);
});

var app = builder.Build();

ConfiguracaoDeEmail.Validar(app.Configuration["Email:Provider"], app.Environment.IsDevelopment());
ConfiguracaoDeAutenticacao.Validar(
    app.Services.GetRequiredService<IOptions<AutenticacaoOptions>>().Value.LegacyCookieName);

var pastaMidia = ArmazenamentoDeMidiaLocal.ObterPasta(app.Configuration);
Directory.CreateDirectory(pastaMidia);

app.UseForwardedHeaders();
app.UseCors("web");
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(pastaMidia),
    RequestPath = "/media",
    OnPrepareResponse = contexto =>
    {
        contexto.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
    }
});
app.UseMiddleware<MetricasDeRequisicaoMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<ProtecaoCsrfMiddleware>();
app.UseAuthentication();
app.UseMiddleware<SessaoIdentityMiddleware>();
app.UseMiddleware<ConsentimentoMiddleware>();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapGet("/ready", async (FisgaDbContext db, CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { status = "ready" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
    .AllowAnonymous();

app.MapAuthEndpoints();
app.MapContaEndpoints();
app.MapNewsletterEndpoints();
app.MapPescariaEndpoints();
app.MapMidiaEndpoints();
app.MapCondicoesEndpoints();
app.MapPontosEndpoints();
app.MapTrilhasEndpoints();
app.MapVisionEndpoints();

await app.Services.InicializarBancoAsync();

app.Run();

static double TaxaDeAmostragem(IWebHostEnvironment ambiente)
{
    var configurada = Environment.GetEnvironmentVariable("SENTRY_TRACES_SAMPLE_RATE");
    if (double.TryParse(configurada, NumberStyles.Float, CultureInfo.InvariantCulture, out var taxa))
    {
        return Math.Clamp(taxa, 0, 1);
    }

    return ambiente.IsDevelopment() ? 1.0 : 0.1;
}

static void AdicionarPolitica(
    Microsoft.AspNetCore.RateLimiting.RateLimiterOptions opcoes,
    IConfiguration configuration,
    string nome,
    int limitePadraoPorMinuto)
{
    var limitePorMinuto = configuration.GetValue<int?>($"RateLimiting:{nome}") ?? limitePadraoPorMinuto;
    opcoes.AddPolicy(nome, contexto =>
    {
        var chave = contexto.Connection.RemoteIpAddress?.ToString() ?? "anonimo";
        return RateLimitPartition.GetFixedWindowLimiter(chave, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limitePorMinuto,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
}

public partial class Program;

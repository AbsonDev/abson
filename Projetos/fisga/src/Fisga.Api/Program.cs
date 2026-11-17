using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Fisga.Api.Endpoints;
using Fisga.Api.Middleware;
using Fisga.Application;
using Fisga.Infrastructure;
using Fisga.Infrastructure.Midia;
using Fisga.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(opcoes =>
{
    opcoes.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    opcoes.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AdicionarAplicacao();
builder.Services.AdicionarInfraestrutura(builder.Configuration);

var origens = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(opcoes =>
{
    opcoes.AddPolicy("web", politica =>
    {
        if (origens.Length == 0)
        {
            politica.AllowAnyOrigin();
        }
        else
        {
            politica.WithOrigins(origens);
        }

        politica.AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddRateLimiter(opcoes =>
{
    opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    AdicionarPolitica(opcoes, "leitura", 180);
    AdicionarPolitica(opcoes, "escrita", 40);
    AdicionarPolitica(opcoes, "pdf", 30);
    AdicionarPolitica(opcoes, "midia", 30);
});

var app = builder.Build();

var pastaMidia = ArmazenamentoDeMidiaLocal.ObterPasta(app.Configuration);
Directory.CreateDirectory(pastaMidia);

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
app.UseMiddleware<ExceptionHandlingMiddleware>();
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

app.MapPescariaEndpoints();
app.MapMidiaEndpoints();

await app.Services.InicializarBancoAsync();

app.Run();

static void AdicionarPolitica(Microsoft.AspNetCore.RateLimiting.RateLimiterOptions opcoes, string nome, int limitePorMinuto)
{
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

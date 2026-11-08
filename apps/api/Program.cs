using System.Net.Mail;
using System.Text;
using System.Threading.RateLimiting;
using Abson.Api.Data;
using Abson.Api.Endpoints;
using Abson.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
{
    if (EF.IsDesignTime)
    {
        jwtSecret = "design-time-only-secret-key-change-me-1234567890";
    }
    else
    {
        throw new InvalidOperationException("Jwt:Secret must be configured with at least 32 characters.");
    }
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "abson-api",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "abson-admin",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("contact", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

var connectionString =
    builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=abson;Username=abson;Password=abson";

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"]
    ?? "https://app.abson.tech,http://localhost:4321,http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseForwardedHeaders();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "abson-api",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapGet("/ready", async (AppDbContext db, CancellationToken ct) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync(ct);
        return canConnect
            ? Results.Ok(new { status = "ready", database = "up" })
            : Results.Json(new { status = "not-ready", database = "down" }, statusCode: 503);
    }
    catch
    {
        return Results.Json(new { status = "not-ready", database = "down" }, statusCode: 503);
    }
});

app.MapGet("/api/site", async (AppDbContext db, HttpResponse response) =>
{
    var content = await db.SiteContents.AsNoTracking().FirstOrDefaultAsync(item => item.Id == 1);
    if (content is null)
    {
        return Results.NotFound();
    }

    response.Headers["Cache-Control"] = "public, max-age=30";
    return Results.Content(content.ContentJson, "application/json");
});

app.MapGet("/api/hello", () => Results.Ok(new
{
    message = "API .NET 10 online",
    service = "abson-api",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapPost("/api/contact", async (
    ContactRequest input,
    AppDbContext db,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!string.IsNullOrWhiteSpace(input.Website))
    {
        return Results.Ok(new { message = "Mensagem recebida com sucesso." });
    }

    var errors = new Dictionary<string, string[]>();

    var name = input.Name?.Trim() ?? string.Empty;
    if (name.Length is < 2 or > 120)
    {
        errors["name"] = ["Informe um nome entre 2 e 120 caracteres."];
    }

    var email = input.Email?.Trim() ?? string.Empty;
    if (!MailAddress.TryCreate(email, out _))
    {
        errors["email"] = ["Informe um e-mail válido."];
    }

    var subjectValue = input.Subject?.Trim() ?? string.Empty;
    if (subjectValue.Length is 0 or > 120)
    {
        errors["subject"] = ["Escolha o tipo de conversa."];
    }

    var message = input.Message?.Trim() ?? string.Empty;
    if (message.Length is < 20 or > 4000)
    {
        errors["message"] = ["A mensagem precisa ter entre 20 e 4000 caracteres."];
    }

    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var subjectLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["novo"] = "Novo sistema",
        ["melhoria"] = "Melhoria em sistema existente",
        ["integracao"] = "Integração entre sistemas",
        ["consultoria"] = "Consultoria pontual",
    };

    var contact = new ContactMessage
    {
        Name = name,
        Email = email,
        Subject = subjectLabels.GetValueOrDefault(subjectValue, subjectValue),
        Message = message,
    };

    db.ContactMessages.Add(contact);
    await db.SaveChangesAsync(ct);

    logger.LogInformation("Nova mensagem de contato {Id} de {Email}", contact.Id, contact.Email);

    return Results.Created($"/api/contact/{contact.Id}", new
    {
        id = contact.Id,
        message = "Mensagem recebida com sucesso."
    });
}).RequireRateLimiting("contact");

app.MapGet("/api/todos", async (AppDbContext db, CancellationToken ct) =>
    Results.Ok(await db.Todos.OrderBy(todo => todo.Id).ToListAsync(ct)));

app.MapPost("/api/todos", async (TodoItem input, AppDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(input.Title))
    {
        return Results.BadRequest(new { error = "title is required" });
    }

    var todo = new TodoItem { Title = input.Title.Trim(), IsDone = input.IsDone };
    db.Todos.Add(todo);
    await db.SaveChangesAsync(ct);

    return Results.Created($"/api/todos/{todo.Id}", todo);
});

app.MapPatch("/api/todos/{id:int}", async (int id, TodoItem input, AppDbContext db, CancellationToken ct) =>
{
    var todo = await db.Todos.FindAsync(new object?[] { id }, ct);
    if (todo is null)
    {
        return Results.NotFound();
    }

    if (!string.IsNullOrWhiteSpace(input.Title))
    {
        todo.Title = input.Title.Trim();
    }

    todo.IsDone = input.IsDone;
    await db.SaveChangesAsync(ct);

    return Results.Ok(todo);
});

app.MapDelete("/api/todos/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
{
    var todo = await db.Todos.FindAsync(new object?[] { id }, ct);
    if (todo is null)
    {
        return Results.NotFound();
    }

    db.Todos.Remove(todo);
    await db.SaveChangesAsync(ct);

    return Results.NoContent();
});

app.MapAdminEndpoints();

if (!EF.IsDesignTime && app.Configuration.GetValue("Database:AutoMigrate", true))
{
    await ApplyMigrationsAsync(app);
}

if (!EF.IsDesignTime)
{
    await SeedData.SeedAsync(app);
}

app.Run();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");

    for (var attempt = 1; attempt <= 12; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied");
            return;
        }
        catch (Exception ex) when (attempt < 12)
        {
            logger.LogWarning(ex, "Database not ready (attempt {Attempt}/12), retrying in 5s", attempt);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }

    await db.Database.MigrateAsync();
}

public sealed record ContactRequest(
    string? Name,
    string? Email,
    string? Subject,
    string? Message,
    string? Website);

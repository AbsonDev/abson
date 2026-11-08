using Abson.Api.Data;
using Abson.Api.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

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

app.UseCors();

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

app.MapGet("/api/hello", () => Results.Ok(new
{
    message = "API .NET 10 online",
    service = "abson-api",
    timestamp = DateTimeOffset.UtcNow
}));

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

if (!EF.IsDesignTime && app.Configuration.GetValue("Database:AutoMigrate", true))
{
    await ApplyMigrationsAsync(app);
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

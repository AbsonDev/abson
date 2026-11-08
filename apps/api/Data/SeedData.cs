using System.Text.Json;
using Abson.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Abson.Api.Data;

public static class SeedData
{
    public static async Task SeedAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AdminUser>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");

        var email = config["Admin:Email"]?.Trim().ToLowerInvariant();
        var password = config["Admin:Password"];

        if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
        {
            var exists = await db.AdminUsers.AnyAsync(user => user.Email == email);
            if (!exists)
            {
                var admin = new AdminUser
                {
                    Email = email,
                    Name = config["Admin:Name"] ?? "Administrador"
                };
                admin.PasswordHash = hasher.HashPassword(admin, password);
                db.AdminUsers.Add(admin);
                await db.SaveChangesAsync();
                logger.LogInformation("Admin user seeded: {Email}", email);
            }
        }
        else
        {
            logger.LogWarning("Admin:Email/Admin:Password not configured; admin seeding skipped.");
        }

        if (!await db.SiteContents.AnyAsync(content => content.Id == 1))
        {
            var seedPath = Path.Combine(AppContext.BaseDirectory, "Seed", "site-content.json");
            if (File.Exists(seedPath))
            {
                var json = await File.ReadAllTextAsync(seedPath);
                using var document = JsonDocument.Parse(json);
                db.SiteContents.Add(new SiteContent { Id = 1, ContentJson = json });
                await db.SaveChangesAsync();
                logger.LogInformation("Site content seeded from {Path}", seedPath);
            }
            else
            {
                logger.LogWarning("Seed content not found at {Path}", seedPath);
            }
        }
    }
}

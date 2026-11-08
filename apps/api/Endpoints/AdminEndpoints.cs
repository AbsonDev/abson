using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Abson.Api.Data;
using Abson.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Abson.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        app.MapPost("/api/admin/login", Login).RequireRateLimiting("login");
        app.MapGet("/api/admin/me", Me).RequireAuthorization();
        app.MapPost("/api/admin/change-password", ChangePassword).RequireAuthorization();
        app.MapGet("/api/admin/content", GetContent).RequireAuthorization();
        app.MapPut("/api/admin/content", PutContent).RequireAuthorization();
        app.MapGet("/api/admin/messages", GetMessages).RequireAuthorization();
        app.MapPatch("/api/admin/messages/{id:int}", UpdateMessage).RequireAuthorization();
        app.MapDelete("/api/admin/messages/{id:int}", DeleteMessage).RequireAuthorization();
        app.MapGet("/api/admin/stats", Stats).RequireAuthorization();
    }

    private static async Task<IResult> Login(
        LoginRequest input,
        AppDbContext db,
        IPasswordHasher<AdminUser> hasher,
        IConfiguration config)
    {
        var email = input.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var user = await db.AdminUsers.FirstOrDefaultAsync(candidate => candidate.Email == email && candidate.IsActive);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, input.Password ?? string.Empty);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Results.Unauthorized();
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            token = CreateToken(user, config),
            expiresAt = DateTimeOffset.UtcNow.AddHours(8),
            user = new { user.Id, user.Name, user.Email }
        });
    }

    private static async Task<IResult> Me(ClaimsPrincipal principal, AppDbContext db)
    {
        var id = CurrentUserId(principal);
        var user = id is null ? null : await db.AdminUsers.FindAsync(id.Value);
        return user is null
            ? Results.Unauthorized()
            : Results.Ok(new { user.Id, user.Name, user.Email, user.LastLoginAt });
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest input,
        ClaimsPrincipal principal,
        AppDbContext db,
        IPasswordHasher<AdminUser> hasher)
    {
        var id = CurrentUserId(principal);
        var user = id is null ? null : await db.AdminUsers.FindAsync(id.Value);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, input.CurrentPassword ?? string.Empty);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Results.BadRequest(new { error = "Senha atual incorreta." });
        }

        if (string.IsNullOrWhiteSpace(input.NewPassword) || input.NewPassword.Length < 10)
        {
            return Results.BadRequest(new { error = "A nova senha precisa ter pelo menos 10 caracteres." });
        }

        user.PasswordHash = hasher.HashPassword(user, input.NewPassword);
        await db.SaveChangesAsync();

        return Results.Ok(new { message = "Senha alterada com sucesso." });
    }

    private static async Task<IResult> GetContent(AppDbContext db)
    {
        var record = await db.SiteContents.AsNoTracking().FirstOrDefaultAsync(content => content.Id == 1);
        if (record is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new
        {
            content = JsonDocument.Parse(record.ContentJson).RootElement,
            record.UpdatedAt,
            record.UpdatedBy
        });
    }

    private static async Task<IResult> PutContent(
        JsonElement input,
        ClaimsPrincipal principal,
        AppDbContext db)
    {
        var content = input.ValueKind == JsonValueKind.Object && input.TryGetProperty("content", out var wrapped)
            ? wrapped
            : input;

        if (content.ValueKind != JsonValueKind.Object)
        {
            return Results.BadRequest(new { error = "Conteúdo inválido." });
        }

        var requiredSections = new[]
        {
            "seo", "profile", "workCard", "about", "skills",
            "experience", "projects", "education", "process", "contact"
        };

        foreach (var section in requiredSections)
        {
            if (!content.TryGetProperty(section, out _))
            {
                return Results.BadRequest(new { error = $"A seção '{section}' é obrigatória." });
            }
        }

        var json = content.GetRawText();
        var record = await db.SiteContents.FirstOrDefaultAsync(item => item.Id == 1);

        if (record is null)
        {
            record = new SiteContent { Id = 1, ContentJson = json, UpdatedBy = CurrentUserId(principal) };
            db.SiteContents.Add(record);
        }
        else
        {
            record.ContentJson = json;
            record.UpdatedAt = DateTimeOffset.UtcNow;
            record.UpdatedBy = CurrentUserId(principal);
        }

        await db.SaveChangesAsync();
        return Results.Ok(new { message = "Conteúdo salvo com sucesso.", record.UpdatedAt });
    }

    private static async Task<IResult> GetMessages(
        AppDbContext db,
        string? status,
        int page = 1,
        int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.ContactMessages.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(message => message.Status == status);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(message => message.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Results.Ok(new { total, page, pageSize, items });
    }

    private static async Task<IResult> UpdateMessage(
        int id,
        UpdateMessageStatusRequest input,
        AppDbContext db)
    {
        var allowed = new[] { "new", "read", "replied", "archived" };
        if (!allowed.Contains(input.Status))
        {
            return Results.BadRequest(new { error = "Status inválido." });
        }

        var message = await db.ContactMessages.FindAsync(id);
        if (message is null)
        {
            return Results.NotFound();
        }

        message.Status = input.Status;
        message.ReadAt = input.Status == "new" ? null : message.ReadAt ?? DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(new { message.Id, message.Status, message.ReadAt });
    }

    private static async Task<IResult> DeleteMessage(int id, AppDbContext db)
    {
        var message = await db.ContactMessages.FindAsync(id);
        if (message is null)
        {
            return Results.NotFound();
        }

        db.ContactMessages.Remove(message);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> Stats(AppDbContext db)
    {
        var total = await db.ContactMessages.CountAsync();
        var unread = await db.ContactMessages.CountAsync(message => message.Status == "new");
        var content = await db.SiteContents.AsNoTracking().FirstOrDefaultAsync(item => item.Id == 1);

        return Results.Ok(new
        {
            messages = new { total, unread },
            contentUpdatedAt = content?.UpdatedAt
        });
    }

    private static int? CurrentUserId(ClaimsPrincipal principal)
    {
        return int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }

    private static string CreateToken(AdminUser user, IConfiguration config)
    {
        var secret = config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret não configurado.");
        var issuer = config["Jwt:Issuer"] ?? "abson-api";
        var audience = config["Jwt:Audience"] ?? "abson-admin";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed record LoginRequest(string? Email, string? Password);
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
public sealed record UpdateMessageStatusRequest(string Status);

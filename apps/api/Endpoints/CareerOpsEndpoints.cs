using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Abson.Api.Data;
using Abson.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Abson.Api.Endpoints;

public static class CareerOpsEndpoints
{
    private static readonly string[] CanonicalStatuses =
    {
        "Evaluated", "Applied", "Responded", "Assessment", "Interview",
        "Offer", "Hired", "Rejected", "Discarded", "SKIP"
    };

    private static readonly string[] AllowedActionTypes = { "set_status", "add_note", "add_pipeline_url" };

    public static void MapCareerOpsEndpoints(this WebApplication app)
    {
        app.MapPut("/api/career-ops/snapshot", PutSnapshot).RequireRateLimiting("career-ops-sync");
        app.MapGet("/api/career-ops/actions/pending", PendingActions).RequireRateLimiting("career-ops-sync");
        app.MapPost("/api/career-ops/actions/{id:int}/result", ActionResult).RequireRateLimiting("career-ops-sync");

        app.MapGet("/api/admin/career-ops", GetCareerOps).RequireAuthorization();
        app.MapPost("/api/admin/career-ops/actions", CreateAction).RequireAuthorization();
    }

    private static async Task<IResult> PutSnapshot(
        HttpRequest request,
        JsonElement input,
        AppDbContext db,
        IConfiguration config)
    {
        var auth = CheckSyncAuth(request, config);
        if (auth is not null)
        {
            return auth;
        }

        if (input.ValueKind != JsonValueKind.Object)
        {
            return Results.BadRequest(new { error = "Snapshot inválido." });
        }

        var raw = input.GetRawText();
        if (raw.Length > 2_000_000)
        {
            return Results.BadRequest(new { error = "Snapshot muito grande (limite de 2 MB)." });
        }

        var schemaVersion = 1;
        if (input.TryGetProperty("schemaVersion", out var versionElement) &&
            versionElement.TryGetInt32(out var parsedVersion) &&
            parsedVersion > 0)
        {
            schemaVersion = parsedVersion;
        }

        var record = await db.CareerOpsSnapshots.FirstOrDefaultAsync(item => item.Id == 1);
        if (record is null)
        {
            record = new CareerOpsSnapshot { Id = 1 };
            db.CareerOpsSnapshots.Add(record);
        }

        record.SnapshotJson = raw;
        record.SchemaVersion = schemaVersion;
        record.Source = "career-ops";
        record.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(new { message = "Snapshot atualizado.", updatedAt = record.UpdatedAt });
    }

    private static async Task<IResult> PendingActions(
        HttpRequest request,
        AppDbContext db,
        IConfiguration config)
    {
        var auth = CheckSyncAuth(request, config);
        if (auth is not null)
        {
            return auth;
        }

        var items = await db.CareerOpsActions
            .AsNoTracking()
            .Where(action => action.Status == "pending")
            .OrderBy(action => action.CreatedAt)
            .Take(50)
            .ToListAsync();

        return Results.Ok(new
        {
            items = items.Select(action => new
            {
                id = action.Id,
                type = action.ActionType,
                payload = ParseJson(action.PayloadJson),
                createdAt = action.CreatedAt
            })
        });
    }

    private static async Task<IResult> ActionResult(
        int id,
        CareerOpsActionResultRequest input,
        HttpRequest request,
        AppDbContext db,
        IConfiguration config)
    {
        var auth = CheckSyncAuth(request, config);
        if (auth is not null)
        {
            return auth;
        }

        var status = input.Status?.Trim().ToLowerInvariant();
        if (status != "applied" && status != "failed")
        {
            return Results.BadRequest(new { error = "Status inválido. Use applied ou failed." });
        }

        var action = await db.CareerOpsActions.FindAsync(id);
        if (action is null)
        {
            return Results.NotFound();
        }

        action.Status = status;
        action.Result = Truncate(input.Result, 2000);
        action.AppliedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(new { action.Id, action.Status, action.AppliedAt });
    }

    private static async Task<IResult> GetCareerOps(AppDbContext db)
    {
        var snapshot = await db.CareerOpsSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == 1);

        var actions = await db.CareerOpsActions
            .AsNoTracking()
            .OrderByDescending(action => action.CreatedAt)
            .Take(25)
            .ToListAsync();

        var pendingActions = await db.CareerOpsActions.CountAsync(action => action.Status == "pending");

        return Results.Ok(new
        {
            snapshot = snapshot is null ? (JsonElement?)null : ParseJson(snapshot.SnapshotJson),
            updatedAt = snapshot?.UpdatedAt,
            schemaVersion = snapshot?.SchemaVersion,
            pendingActions,
            actions = actions.Select(action => new
            {
                id = action.Id,
                type = action.ActionType,
                status = action.Status,
                result = action.Result,
                payload = ParseJson(action.PayloadJson),
                createdAt = action.CreatedAt,
                appliedAt = action.AppliedAt
            })
        });
    }

    private static async Task<IResult> CreateAction(
        JsonElement input,
        ClaimsPrincipal principal,
        AppDbContext db)
    {
        if (input.ValueKind != JsonValueKind.Object)
        {
            return Results.BadRequest(new { error = "Ação inválida." });
        }

        var type = input.TryGetProperty("type", out var typeElement) ? typeElement.GetString()?.Trim() : null;
        if (string.IsNullOrWhiteSpace(type) || !AllowedActionTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = "Tipo de ação inválido." });
        }

        if (!input.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
        {
            return Results.BadRequest(new { error = "Payload inválido." });
        }

        var payloadRaw = payload.GetRawText();
        if (payloadRaw.Length > 8_000)
        {
            return Results.BadRequest(new { error = "Payload muito grande (limite de 8 KB)." });
        }

        var validationError = ValidatePayload(type, payload);
        if (validationError is not null)
        {
            return Results.BadRequest(new { error = validationError });
        }

        var pendingCount = await db.CareerOpsActions.CountAsync(action => action.Status == "pending");
        if (pendingCount >= 100)
        {
            return Results.Json(
                new { error = "Há muitas ações pendentes. Aguarde o próximo sync do Mac." },
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        var action = new CareerOpsAction
        {
            ActionType = type,
            PayloadJson = payloadRaw,
            Status = "pending",
            CreatedBy = CurrentUserId(principal)
        };

        db.CareerOpsActions.Add(action);
        await db.SaveChangesAsync();

        return Results.Created(
            $"/api/admin/career-ops/actions/{action.Id}",
            new { id = action.Id, status = action.Status, createdAt = action.CreatedAt });
    }

    private static string? ValidatePayload(string type, JsonElement payload)
    {
        switch (type.ToLowerInvariant())
        {
            case "set_status":
            {
                if (string.IsNullOrWhiteSpace(GetString(payload, "report")) &&
                    string.IsNullOrWhiteSpace(GetString(payload, "company")))
                {
                    return "Informe o número do relatório ou a empresa.";
                }

                var status = GetString(payload, "status");
                if (string.IsNullOrWhiteSpace(status) ||
                    !CanonicalStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
                {
                    return "Status inválido.";
                }

                var note = GetString(payload, "note");
                if (note is { Length: > 500 })
                {
                    return "A nota deve ter no máximo 500 caracteres.";
                }

                return null;
            }

            case "add_note":
            {
                if (string.IsNullOrWhiteSpace(GetString(payload, "report")) &&
                    string.IsNullOrWhiteSpace(GetString(payload, "company")))
                {
                    return "Informe o número do relatório ou a empresa.";
                }

                var note = GetString(payload, "note");
                if (string.IsNullOrWhiteSpace(note))
                {
                    return "Informe a nota.";
                }

                return note.Length > 500 ? "A nota deve ter no máximo 500 caracteres." : null;
            }

            case "add_pipeline_url":
            {
                var url = GetString(payload, "url");
                if (string.IsNullOrWhiteSpace(url) ||
                    !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    return "Informe uma URL http(s) válida.";
                }

                if (url.Length > 2000)
                {
                    return "URL muito longa.";
                }

                var company = GetString(payload, "company");
                if (company is { Length: > 200 })
                {
                    return "O nome da empresa deve ter no máximo 200 caracteres.";
                }

                var role = GetString(payload, "role");
                if (role is { Length: > 200 })
                {
                    return "O cargo deve ter no máximo 200 caracteres.";
                }

                return null;
            }

            default:
                return "Tipo de ação inválido.";
        }
    }

    private static IResult? CheckSyncAuth(HttpRequest request, IConfiguration config)
    {
        var expected = config["CareerOps:SyncToken"];
        if (string.IsNullOrWhiteSpace(expected))
        {
            return Results.Json(
                new { error = "Sincronização não configurada no servidor (CareerOps:SyncToken)." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var provided = request.Headers["X-Career-Ops-Token"].ToString();
        if (string.IsNullOrWhiteSpace(provided))
        {
            return Results.Unauthorized();
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var matches = expectedBytes.Length == providedBytes.Length &&
                      CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);

        return matches ? null : Results.Unauthorized();
    }

    private static int? CurrentUserId(ClaimsPrincipal principal)
    {
        return int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }

    private static string? GetString(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static JsonElement ParseJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var empty = JsonDocument.Parse("{}");
            return empty.RootElement.Clone();
        }
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}

public sealed record CareerOpsActionResultRequest(string? Status, string? Result);

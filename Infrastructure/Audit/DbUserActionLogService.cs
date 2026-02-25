using System.Text.Json;
using System.Text.Json.Serialization;
using BonyadRazi.Portal.Application.Abstractions;
using BonyadRazi.Portal.Infrastructure.Audit.Entities;
using BonyadRazi.Portal.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace BonyadRazi.Portal.Infrastructure.Audit;

public sealed class DbUserActionLogService : IUserActionLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RasfPortalDbContext _db;
    private readonly ILogger<DbUserActionLogService> _logger;

    public DbUserActionLogService(RasfPortalDbContext db, ILogger<DbUserActionLogService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(Guid? userId, string actionType, object metadata, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actionType))
            throw new ArgumentException("actionType is required.", nameof(actionType));

        string metadataJson;
        try
        {
            if (metadata is string s && LooksLikeJson(s))
                metadataJson = s;
            else
                metadataJson = JsonSerializer.Serialize(metadata ?? new { }, JsonOptions);
        }
        catch (Exception ex)
        {
            // fail-safe: serialization must never break requests
            _logger.LogWarning(ex, "Failed to serialize audit metadata. actionType={ActionType}", actionType);
            metadataJson = "{\"serializationError\":true}";
        }

        var entity = new UserActionLog
        {
            UserId = userId,
            ActionType = actionType.Trim(),
            MetadataJson = metadataJson
        };

        // ---- Best-effort enrichment from metadataJson ----
        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var root = doc.RootElement;

                entity.Utc = TryGetDateTime(root, "utc") ?? entity.Utc;
                entity.StatusCode = TryGetInt(root, "statusCode");
                entity.Method = TryGetString(root, "method");
                entity.Path = TryGetString(root, "path");
                entity.TraceId = TryGetString(root, "traceId");
                entity.RemoteIp = TryGetString(root, "ip") ?? TryGetString(root, "remoteIp");
                entity.UserAgent = TryGetString(root, "userAgent");
                entity.Reason = TryGetString(root, "reason");

                entity.CompanyCode =
                    TryGetGuid(root, "company_code")
                    ?? TryGetGuid(root, "companyCode");
            }
        }
        catch (Exception ex)
        {
            // fail-safe: enrichment must never break requests
            _logger.LogDebug(ex, "Failed to enrich audit log fields from metadataJson. actionType={ActionType}", actionType);
        }

        try
        {
            _db.UserActionLogs.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // fail-safe: audit write failures must not break the request pipeline
            _logger.LogError(ex, "Failed to write audit log. actionType={ActionType}", actionType);
        }
    }

    private static bool LooksLikeJson(string s)
    {
        s = s.Trim();
        return (s.StartsWith("{") && s.EndsWith("}")) || (s.StartsWith("[") && s.EndsWith("]"));
    }

    private static string? TryGetString(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var v)) return null;
        if (v.ValueKind == JsonValueKind.String) return v.GetString();
        if (v.ValueKind == JsonValueKind.Number) return v.GetRawText();
        if (v.ValueKind is JsonValueKind.True or JsonValueKind.False) return v.GetBoolean().ToString();
        return v.GetRawText();
    }

    private static int? TryGetInt(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var v)) return null;

        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n))
            return n;

        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var s))
            return s;

        return null;
    }

    private static Guid? TryGetGuid(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var v)) return null;

        if (v.ValueKind == JsonValueKind.String && Guid.TryParse(v.GetString(), out var g))
            return g;

        return null;
    }

    private static DateTime? TryGetDateTime(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var v)) return null;

        if (v.ValueKind == JsonValueKind.String && DateTime.TryParse(v.GetString(), out var dt))
            return dt;

        return null;
    }
}
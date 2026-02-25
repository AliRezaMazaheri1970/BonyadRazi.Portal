using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace BonyadRazi.Portal.Api.Audit;

public static class RequestAuditMetadataFactory
{
    public static IReadOnlyDictionary<string, object?> Create(
        HttpContext context,
        IDictionary<string, object?>? customValues = null)
    {
        var ip = ResolveClientIp(context);
        var userAgent = context.Request.Headers.UserAgent.ToString();

        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ip"] = ip,
            ["userAgent"] = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent,
            ["path"] = context.Request.Path.ToString(),
            ["method"] = context.Request.Method,
            ["traceId"] = context.TraceIdentifier,
            ["utc"] = DateTime.UtcNow,
            ["sub"] = context.User.FindFirst("sub")?.Value
        };

        if (customValues is not null)
            foreach (var kv in customValues)
                metadata[kv.Key] = kv.Value;

        return metadata;
    }

    public static Guid? ResolveAuthenticatedUserId(ClaimsPrincipal? user)
    {
        if (user is null) return null;

        var raw = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static string ResolveClientIp(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded))
        {
            var first = forwarded.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(first) && IPAddress.TryParse(first, out _))
                return first;
        }

        var ip = context.Connection.RemoteIpAddress;
        if (ip is null) return "unknown";
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        return ip.ToString();
    }
}
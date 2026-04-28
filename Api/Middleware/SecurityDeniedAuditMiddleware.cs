using System.Net;
using System.Security.Claims;
using BonyadRazi.Portal.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BonyadRazi.Portal.Api.Middleware;

public sealed class SecurityDeniedAuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityDeniedAuditMiddleware> _logger;

    public SecurityDeniedAuditMiddleware(
        RequestDelegate next,
        ILogger<SecurityDeniedAuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IUserActionLogService userActionLogService)
    {
        await _next(context);

        // Avoid recursive auditing of the audit report endpoint itself.
        if (context.Request.Path.StartsWithSegments("/api/audit"))
            return;

        var status = context.Response.StatusCode;

        if (status is not (StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden))
            return;

        Guid? actorUserId =
            TryGetGuidClaim(context.User, "sub") ??
            TryGetGuidClaim(context.User, ClaimTypes.NameIdentifier);

        Guid? companyCode = TryGetGuidClaim(context.User, "company_code");

        var metadata = new
        {
            utc = DateTime.UtcNow,

            statusCode = status,
            reason = status == StatusCodes.Status401Unauthorized
                ? "UNAUTHORIZED_401"
                : "FORBIDDEN_403",

            method = context.Request.Method,
            path = context.Request.Path.ToString(),
            queryString = RedactQueryString(context.Request.QueryString.ToString()),

            traceId = context.TraceIdentifier,

            remoteIp = ResolveClientIp(context),
            userAgent = Truncate(context.Request.Headers.UserAgent.ToString(), 512),

            companyCode
        };

        try
        {
            await userActionLogService.LogAsync(
                actorUserId,
                "SecurityDenied",
                metadata,
                context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Request was aborted by client/server. Do not fail the request pipeline.
        }
        catch (Exception ex)
        {
            // Audit must be fail-safe and must not break the original request.
            _logger.LogError(ex, "Failed to write SecurityDenied audit log.");
        }
    }

    private static Guid? TryGetGuidClaim(ClaimsPrincipal user, string claimType)
    {
        var raw = user.FindFirst(claimType)?.Value;
        return Guid.TryParse(raw, out var g) ? g : null;
    }

    private static string ResolveClientIp(HttpContext context)
    {
        try
        {
            // After app.UseForwardedHeaders(), RemoteIpAddress should already be the real client IP,
            // but only if the request came from a trusted KnownProxy/KnownNetwork.
            var ip = context.Connection.RemoteIpAddress;

            if (ip is null)
                return "unknown";

            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();

            return ip.ToString();
        }
        catch
        {
            return "unknown";
        }
    }

    private static string RedactQueryString(string? queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
            return string.Empty;

        var value = queryString.Trim();

        // Never store secrets/tokens/passwords in audit metadata.
        var sensitiveKeys = new[]
        {
            "password",
            "pass",
            "pwd",
            "token",
            "access_token",
            "refresh_token",
            "authorization",
            "secret",
            "key"
        };

        foreach (var key in sensitiveKeys)
        {
            if (value.Contains(key, StringComparison.OrdinalIgnoreCase))
                return "[REDACTED]";
        }

        return Truncate(value, 1024);
    }

    private static string Truncate(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = value.Trim();

        return value.Length <= maxLen
            ? value
            : value[..maxLen];
    }
}
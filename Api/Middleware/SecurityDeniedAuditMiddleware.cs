using BonyadRazi.Portal.Api.Audit;
using BonyadRazi.Portal.Application.Abstractions;
using System.Security.Claims;

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

        // Avoid recursive auditing of audit endpoints.
        if (context.Request.Path.StartsWithSegments("/api/audit"))
        {
            return;
        }

        var status = context.Response.StatusCode;

        if (status is not (StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden))
        {
            return;
        }

        Guid? actorUserId =
            TryGetGuidClaim(context.User, "sub") ??
            TryGetGuidClaim(context.User, ClaimTypes.NameIdentifier);

        Guid? companyCode = TryGetGuidClaim(context.User, "company_code");

        var remoteIp = ResolveClientIp(context);
        var userAgent = Truncate(context.Request.Headers.UserAgent.ToString(), 512);
        var path = context.Request.Path.ToString();
        var method = context.Request.Method;
        var reason = status == StatusCodes.Status401Unauthorized
            ? "UNAUTHORIZED_401"
            : "FORBIDDEN_403";

        var metadata = new
        {
            utc = DateTime.UtcNow,

            statusCode = status,
            reason,

            method,
            path,
            queryString = RedactQueryString(context.Request.QueryString.ToString()),

            traceId = context.TraceIdentifier,

            remoteIp,
            userAgent,

            companyCode,

            // Safe diagnostic data only.
            forwardedFor = RedactHeaderValue(context.Request.Headers["X-Forwarded-For"].ToString()),
            forwardedProto = RedactHeaderValue(context.Request.Headers["X-Forwarded-Proto"].ToString()),
            forwardedHost = RedactHeaderValue(context.Request.Headers["X-Forwarded-Host"].ToString())
        };

        try
        {
            await userActionLogService.LogAsync(
                actorUserId,
                AuditActionTypes.SecurityAccessDenied,
                metadata,
                context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Normal request abort. Do not fail the pipeline.
        }
        catch (Exception ex)
        {
            // Audit must be fail-safe.
            _logger.LogError(ex, "Failed to write security access denied audit log.");
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
            var ip = context.Connection.RemoteIpAddress;

            if (ip is null)
            {
                return "unknown";
            }

            if (ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }

            return ip.ToString();
        }
        catch
        {
            return "unknown";
        }
    }

    private static string RedactQueryString(string? queryString)
    {
        return AuditRedaction.RedactTextIfContainsSensitiveKey(queryString, 1024);
    }

    private static string RedactHeaderValue(string? value)
    {
        return AuditRedaction.RedactTextIfContainsSensitiveKey(value, 512);
    }

    private static string Truncate(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        value = value.Trim();

        return value.Length <= maxLen
            ? value
            : value[..maxLen];
    }
}

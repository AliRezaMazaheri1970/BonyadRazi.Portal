using BonyadRazi.Portal.Api.Audit;
using BonyadRazi.Portal.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace BonyadRazi.Portal.Api.Security;

public sealed class TenantConsistencyFilter : IAsyncActionFilter
{
    private readonly IUserActionLogService _userActionLogService;

    private static readonly HashSet<string> SystemRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "SuperAdmin"
    };

    public TenantConsistencyFilter(IUserActionLogService userActionLogService)
    {
        _userActionLogService = userActionLogService;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var attribute = GetTenantAttribute(context);

        if (attribute is null)
        {
            await next();
            return;
        }

        var user = context.HttpContext.User;

        if (attribute.AllowSystemRoles && IsSystemUser(user))
        {
            await next();
            return;
        }

        if (!context.ActionArguments.TryGetValue(attribute.RouteArgumentName, out var routeValue))
        {
            await WriteTenantViolationAudit(
                context,
                "TENANT_ROUTE_ARGUMENT_MISSING",
                routeCompanyCode: null,
                claimCompanyCode: null);

            context.Result = new ForbidResult();
            return;
        }

        if (!TryReadGuid(routeValue, out var routeCompanyCode))
        {
            await WriteTenantViolationAudit(
                context,
                "TENANT_ROUTE_ARGUMENT_INVALID",
                routeCompanyCode: null,
                claimCompanyCode: null);

            context.Result = new ForbidResult();
            return;
        }

        if (!user.TryGetCompanyCode(out var claimCompanyCode))
        {
            await WriteTenantViolationAudit(
                context,
                "TENANT_CLAIM_MISSING_OR_INVALID",
                routeCompanyCode,
                claimCompanyCode: null);

            context.Result = new ForbidResult();
            return;
        }

        if (claimCompanyCode != routeCompanyCode)
        {
            await WriteTenantViolationAudit(
                context,
                "TENANT_MISMATCH",
                routeCompanyCode,
                claimCompanyCode);

            context.Result = new ForbidResult();
            return;
        }

        await next();
    }

    private async Task WriteTenantViolationAudit(
        ActionExecutingContext context,
        string reason,
        Guid? routeCompanyCode,
        Guid? claimCompanyCode)
    {
        var http = context.HttpContext;

        try
        {
            await _userActionLogService.LogAsync(
                userId: TryGetUserId(http.User),
                actionType: AuditActionTypes.SecurityTenantViolation,
                metadata: new
                {
                    utc = DateTime.UtcNow,
                    statusCode = StatusCodes.Status403Forbidden,
                    reason,

                    method = http.Request.Method,
                    path = http.Request.Path.ToString(),
                    queryString = string.Empty,

                    traceId = http.TraceIdentifier,

                    remoteIp = ResolveClientIp(http),
                    userAgent = Truncate(http.Request.Headers.UserAgent.ToString(), 512),

                    routeCompanyCode,
                    claimCompanyCode
                },
                cancellationToken: http.RequestAborted);
        }
        catch
        {
            // Audit must be fail-safe. Tenant protection must still return 403.
        }
    }

    private static RequireTenantMatchAttribute? GetTenantAttribute(ActionExecutingContext context)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            return null;

        var methodAttribute = descriptor.MethodInfo
            .GetCustomAttributes(typeof(RequireTenantMatchAttribute), inherit: true)
            .OfType<RequireTenantMatchAttribute>()
            .FirstOrDefault();

        if (methodAttribute is not null)
            return methodAttribute;

        return descriptor.ControllerTypeInfo
            .GetCustomAttributes(typeof(RequireTenantMatchAttribute), inherit: true)
            .OfType<RequireTenantMatchAttribute>()
            .FirstOrDefault();
    }

    private static bool IsSystemUser(ClaimsPrincipal user)
    {
        return SystemRoles.Any(user.IsInRole);
    }

    private static bool TryReadGuid(object? value, out Guid guid)
    {
        switch (value)
        {
            case Guid g:
                guid = g;
                return true;

            case string s when Guid.TryParse(s, out var parsed):
                guid = parsed;
                return true;

            default:
                guid = default;
                return false;
        }
    }

    private static Guid? TryGetUserId(ClaimsPrincipal user)
    {
        var value =
            user.FindFirstValue("sub") ??
            user.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var id)
            ? id
            : null;
    }

    private static string ResolveClientIp(HttpContext context)
    {
        try
        {
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
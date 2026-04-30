using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BonyadRazi.Portal.Api.Security;

public sealed class TenantConsistencyFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> SystemRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "SuperAdmin"
    };

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
            context.Result = new ForbidResult();
            return;
        }

        if (!TryReadGuid(routeValue, out var routeCompanyCode))
        {
            context.Result = new ForbidResult();
            return;
        }

        if (!user.TryGetCompanyCode(out var claimCompanyCode))
        {
            context.Result = new ForbidResult();
            return;
        }

        if (claimCompanyCode != routeCompanyCode)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
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

    private static bool IsSystemUser(System.Security.Claims.ClaimsPrincipal user)
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
}
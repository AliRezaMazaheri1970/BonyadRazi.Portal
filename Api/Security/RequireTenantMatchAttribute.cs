namespace BonyadRazi.Portal.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireTenantMatchAttribute : Attribute
{
    public RequireTenantMatchAttribute(string routeArgumentName = "companyCode")
    {
        RouteArgumentName = routeArgumentName;
    }

    public string RouteArgumentName { get; }

    public bool AllowSystemRoles { get; init; } = false;
}
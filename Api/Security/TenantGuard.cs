using System.Security.Claims;

namespace BonyadRazi.Portal.Api.Security;

public static class TenantGuard
{
    public static bool RouteCompanyMatchesClaim(ClaimsPrincipal user, Guid routeCompanyCode)
    {
        return user.TryGetCompanyCode(out var claimCompanyCode) &&
               claimCompanyCode == routeCompanyCode;
    }
}
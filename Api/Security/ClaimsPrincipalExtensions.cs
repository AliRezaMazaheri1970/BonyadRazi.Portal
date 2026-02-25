using System.Security.Claims;

namespace BonyadRazi.Portal.Api.Security;

public static class ClaimsPrincipalExtensions
{
    public static bool TryGetCompanyCode(this ClaimsPrincipal user, out Guid companyCode)
    {
        companyCode = default;

        // Claim name استاندارد پروژه
        var raw = user.FindFirstValue(PortalClaims.CompanyCode)
                  ?? user.FindFirstValue("company_code");

        return Guid.TryParse(raw, out companyCode);
    }
}
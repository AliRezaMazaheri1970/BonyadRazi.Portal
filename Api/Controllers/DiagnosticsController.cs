using BonyadRazi.Portal.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BonyadRazi.Portal.Api.Controllers;

[ApiController]
[Route("api/diagnostics")]
public sealed class DiagnosticsController : ControllerBase
{
    [HttpGet("auth-test")]
    [Authorize]
    public IActionResult AuthTest()
    {
        return Ok(new
        {
            status = "ok",
            where = "api",
            check = "auth",
            user = User.Identity?.Name,
            utc = DateTime.UtcNow
        });
    }

    [HttpGet("admin-test")]
    [Authorize(Policy = PortalPolicies.SystemAdmin)]
    public IActionResult AdminTest()
    {
        return Ok(new
        {
            status = "ok",
            where = "api",
            check = "admin",
            user = User.Identity?.Name,
            utc = DateTime.UtcNow
        });
    }

    [HttpGet("tenant-test/{companyCode:guid}")]
    [Authorize]
    public IActionResult TenantTest(Guid companyCode)
    {
        if (!User.TryGetCompanyCode(out var claimCompanyCode))
        {
            return Forbid();
        }

        var isSystemAdmin =
            User.IsInRole("Admin") ||
            User.IsInRole("SuperAdmin");

        if (claimCompanyCode != companyCode && !isSystemAdmin)
        {
            return Forbid();
        }

        return Ok(new
        {
            status = "ok",
            where = "api",
            check = "tenant",
            routeCompanyCode = companyCode,
            claimCompanyCode,
            utc = DateTime.UtcNow
        });
    }
}
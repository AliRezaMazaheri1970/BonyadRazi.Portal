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
    [RequireTenantMatch]
    public IActionResult TenantTest(Guid companyCode)
    {
        // Defense-in-depth:
        // TenantConsistencyFilter قبل از ورود به action، companyCode را با claim کاربر چک می‌کند.
        // این چک inline فعلاً باقی می‌ماند تا رفتار امنیتی قبلی و تست‌های فعلی حفظ شوند.

        if (!User.TryGetCompanyCode(out var claimCompanyCode))
        {
            return Forbid();
        }

        // این endpoint مخصوص تست strict tenant isolation است.
        // یعنی route companyCode باید دقیقاً با claim شرکت یکی باشد.
        if (claimCompanyCode != companyCode)
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
            tenantMatched = true,
            utc = DateTime.UtcNow
        });
    }
}
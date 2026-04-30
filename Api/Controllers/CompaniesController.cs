using BonyadRazi.Portal.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BonyadRazi.Portal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CompaniesController : ControllerBase
{
    [HttpGet("{companyCode:guid}")]
    [Authorize(Policy = PortalPolicies.CompaniesRead)]
    [RequireTenantMatch]
    public ActionResult<object> GetCompany(Guid companyCode)
    {
        // Defense-in-depth:
        // TenantConsistencyFilter قبل از ورود به action، companyCode را با claim کاربر چک می‌کند.
        // این چک inline فعلاً باقی می‌ماند تا رفتار امنیتی قبلی و تست‌های موجود حفظ شوند.

        if (!(User?.Identity?.IsAuthenticated ?? false))
        {
            return Unauthorized();
        }

        if (!User.TryGetCompanyCode(out var claimCompanyCode))
        {
            return Forbid();
        }

        // این endpoint فعلاً strict tenant-scoped است.
        // یعنی حتی اگر کاربر Admin باشد، route companyCode باید با claim یکی باشد.
        // برای endpointهای آینده مثل /api/companies/directory یا admin-specific routes
        // سیاست جداگانه تعریف می‌کنیم.
        if (claimCompanyCode != companyCode)
        {
            return Forbid();
        }

        return Ok(new
        {
            companyCode,
            claimCompanyCode,
            tenantMatched = true,
            utc = DateTime.UtcNow
        });
    }
}
using BonyadRazi.Portal.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BonyadRazi.Portal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CompaniesController : ControllerBase
{
    [Authorize(Policy = PortalPolicies.CompaniesRead)]
    [HttpGet("{companyCode:guid}")]
    public ActionResult<object> GetCompany(Guid companyCode)
    {
        // اگر به هر دلیلی بدون auth وارد شد
        if (!(User?.Identity?.IsAuthenticated ?? false))
            return Unauthorized();

        // claim company_code باید وجود داشته باشد
        if (!User.TryGetCompanyCode(out var claimCompanyCode))
            return Forbid();

        // Tenant isolation: route باید با claim یکی باشد
        if (claimCompanyCode != companyCode)
            return Forbid();

        return Ok(new { companyCode });
    }
}
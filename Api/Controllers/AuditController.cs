using BonyadRazi.Portal.Api.Audit;
using BonyadRazi.Portal.Api.Security;
using BonyadRazi.Portal.Infrastructure.Persistence;
using BonyadRazi.Portal.Shared.Audit.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BonyadRazi.Portal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuditController : ControllerBase
{
    private readonly RasfPortalDbContext _db;

    public AuditController(RasfPortalDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Denied security events (401/403). Admin-only.
    /// </summary>
    [HttpGet("denied")]
    [Authorize(Policy = PortalPolicies.AuditRead)]
    public async Task<ActionResult<object>> GetDenied(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int? statusCode,
        [FromQuery] Guid? companyCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 50;
        }

        if (pageSize > 500)
        {
            pageSize = 500;
        }

        var q = _db.UserActionLogs.AsNoTracking()
            .Where(x => x.ActionType == AuditActionTypes.SecurityAccessDenied);

        if (fromUtc.HasValue)
        {
            q = q.Where(x => x.Utc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            q = q.Where(x => x.Utc <= toUtc.Value);
        }

        if (statusCode.HasValue)
        {
            q = q.Where(x => x.StatusCode == statusCode.Value);
        }

        if (companyCode.HasValue)
        {
            q = q.Where(x => x.CompanyCode == companyCode.Value);
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(x => x.Utc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AuditDeniedRowDto
            {
                Utc = x.Utc,
                StatusCode = x.StatusCode,

                UserId = x.UserId,
                CompanyCode = x.CompanyCode,

                ActionType = x.ActionType,
                Reason = x.Reason,

                Method = x.Method,
                Path = x.Path,

                RemoteIp = x.RemoteIp,
                UserAgent = x.UserAgent,

                TraceId = x.TraceId
            })
            .ToListAsync(ct);

        return Ok(new
        {
            page,
            pageSize,
            total,
            items
        });
    }
}
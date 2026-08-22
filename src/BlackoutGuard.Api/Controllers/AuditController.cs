using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BlackoutGuard.Infrastructure.Persistence;

namespace BlackoutGuard.Api.Controllers;

[ApiController]
[Route("api/v1/audit")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly BlackoutGuardDbContext _dbContext;

    public AuditController(BlackoutGuardDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] Guid? facilityId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userFacilityId = GetFacilityIdFromClaims();
        if (userFacilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        // Security Check: Query Param ထဲမှာ တခြား Facility ID ပေးပြီး တောင်းဆိုပါက Cross-Tenant Leakage မဖြစ်အောင် 403 ငြင်းပစ်မည်
        if (facilityId.HasValue && facilityId.Value != userFacilityId.Value)
        {
            return StatusCode(403, new { error = "Forbidden: Cannot access audit logs of another facility." });
        }

        var query = _dbContext.DecisionAuditLogs
            .Where(l => l.FacilityId == userFacilityId.Value)
            .OrderByDescending(l => l.TimestampUtc);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                l.Id,
                l.TimestampUtc,
                l.EventType,
                l.Rationale,
                AffectedLoadName = l.AffectedLoad != null ? l.AffectedLoad.Name : "N/A"
            })
            .ToListAsync(ct);

        return Ok(new
        {
            total,
            page,
            pageSize,
            items
        });
    }

    private Guid? GetFacilityIdFromClaims()
    {
        var claimValue = User.FindFirstValue("facility_id") ?? User.FindFirstValue("FacilityId");
        return Guid.TryParse(claimValue, out var facilityId) ? facilityId : null;
    }
}
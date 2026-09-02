using BlackoutGuard.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;

namespace BlackoutGuard.Api.Controllers;

[ApiController]
[Route("api/v1/facilities")]
[Authorize]
public class FacilitiesController : ControllerBase
{
    private readonly BlackoutGuardDbContext _context;

    public FacilitiesController(BlackoutGuardDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get current user's facility information
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetFacilities()
    {
        var facilityId = GetFacilityIdFromClaims();

        if (facilityId == null)
        {
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });
        }

        var facility = await _context.Facilities
            .FirstOrDefaultAsync(f => f.Id == facilityId);

        if (facility == null)
        {
            return NotFound(new { error = "Facility not found." });
        }

        // Return as array to match frontend expectation
        return Ok(new[]
        {
            new
            {
                id = facility.Id.ToString(),
                tenantId = facility.TenantId.ToString(),
                name = facility.Name,
                generatorCapacityKw = facility.GeneratorCapacityKw,
                solarCapacityKw = facility.SolarCapacityKw,
                isGridOnline = facility.IsGridOnline,
                timezoneId = facility.TimezoneId ?? "UTC",
                createdAt = facility.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            }
        });
    }

    /// <summary>
    /// Get facility by ID (optional - for admin use)
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetFacilityById(Guid id)
    {
        var facility = await _context.Facilities
            .FirstOrDefaultAsync(f => f.Id == id);

        if (facility == null)
        {
            return NotFound(new { error = "Facility not found." });
        }

        return Ok(new
        {
            id = facility.Id.ToString(),
            tenantId = facility.TenantId.ToString(),
            name = facility.Name,
            generatorCapacityKw = facility.GeneratorCapacityKw,
            solarCapacityKw = facility.SolarCapacityKw,
            isGridOnline = facility.IsGridOnline,
            timezoneId = facility.TimezoneId ?? "UTC",
            createdAt = facility.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        });
    }

    private Guid? GetFacilityIdFromClaims()
    {
        var claimValue = User.FindFirstValue("facility_id") ??
                         User.FindFirstValue("FacilityId");

        if (string.IsNullOrEmpty(claimValue))
            return null;

        return Guid.TryParse(claimValue, out var facilityId) ? facilityId : null;
    }
}
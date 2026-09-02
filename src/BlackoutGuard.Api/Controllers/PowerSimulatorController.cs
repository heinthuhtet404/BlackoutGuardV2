using BlackoutGuard.Application.Services;
using BlackoutGuard.Domain.Entities;
using BlackoutGuard.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;

namespace BlackoutGuard.Api.Controllers;

[ApiController]
[Route("api/v1/simulator")]
[Authorize(Roles = "Admin")]
public class PowerSimulatorController : ControllerBase
{
    private readonly BlackoutGuardDbContext _context;
    private readonly PowerManagementService _powerService;

    // Memory ထဲတွင် Simulator State သိမ်းထားရန် static variable
    private static PowerSourceState CurrentState = new(
        FacilityId: Guid.Empty,
        IsGridAvailable: true,
        SolarOutputKw: 0,
        GeneratorOutputKw: 0,
        ActiveSource: PowerSourceType.Grid
    );

    public PowerSimulatorController(BlackoutGuardDbContext context, PowerManagementService powerService)
    {
        _context = context;
        _powerService = powerService;
    }

    private Guid? GetFacilityIdFromClaims()
    {
        var claimValue = User.FindFirstValue("facility_id") ??
                         User.FindFirstValue("FacilityId");

        if (string.IsNullOrEmpty(claimValue))
            return null;

        return Guid.TryParse(claimValue, out var facilityId) ? facilityId : null;
    }

    /// <summary>
    /// Front-end Simulator UI မှ DB ထဲရှိ Facility Configuration ကို ဆွဲယူသည့် GET Endpoint
    /// </summary>
    [HttpGet("config")]
    public async Task<IActionResult> GetFacilityConfig()
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
            return NotFound(new { message = "No facility found in the database." });
        }

        // Return full facility info for frontend
        return Ok(new
        {
            id = facility.Id.ToString(),
            tenantId = facility.TenantId.ToString(),
            name = facility.Name,
            gridOnline = facility.IsGridOnline,
            solarCapacityKw = facility.SolarCapacityKw,
            generatorCapacityKw = facility.GeneratorCapacityKw,
            timezoneId = facility.TimezoneId ?? "UTC",
            createdAt = facility.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        });
    }

    /// <summary>
    /// Front-end Simulator UI မှ Grid State, Solar/Generator Capacities များကို
    /// Database (facilities table) ထဲသို့ Save/Update လုပ်ပေးသော API Endpoint
    /// </summary>
    [HttpPost("config")]
    public async Task<IActionResult> UpdateFacilityConfig([FromBody] FacilityConfigUpdateRequest request)
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
            return NotFound(new { message = "No facility found in the database to update configuration." });
        }

        // 1. Database Persistence Model ကို Update လုပ်ခြင်း
        facility.IsGridOnline = request.GridOnline;
        facility.SolarCapacityKw = request.SolarCapacityKw;
        facility.GeneratorCapacityKw = request.GeneratorCapacityKw;

        await _context.SaveChangesAsync();

        // 2. Simulator Static State ကိုပါ Database Update တန်ဖိုးဖြင့် လိုက်ပြောင်းပေးခြင်း
        CurrentState = CurrentState with
        {
            FacilityId = facility.Id,
            IsGridAvailable = facility.IsGridOnline,
            SolarOutputKw = facility.SolarCapacityKw,
            GeneratorOutputKw = facility.GeneratorCapacityKw
        };

        return Ok(new
        {
            message = "Facility power configuration updated successfully in Database.",
            facilityId = facility.Id,
            gridOnline = facility.IsGridOnline,
            solarCapacityKw = facility.SolarCapacityKw,
            generatorCapacityKw = facility.GeneratorCapacityKw
        });
    }

    [HttpGet("state")]
    public async Task<IActionResult> GetCurrentStatus()
    {
        var facilityId = GetFacilityIdFromClaims();

        if (facilityId == null)
        {
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });
        }

        // 1. Database ထဲရှိ Active Facility ၏ Power Configuration ကို ရယူခြင်း
        var facility = await _context.Facilities
            .FirstOrDefaultAsync(f => f.Id == facilityId);

        if (facility != null)
        {
            CurrentState = CurrentState with
            {
                FacilityId = facility.Id,
                IsGridAvailable = facility.IsGridOnline,
                SolarOutputKw = facility.SolarCapacityKw,
                GeneratorOutputKw = facility.GeneratorCapacityKw
            };
        }

        // 2. Fetch DB Models from Infrastructure (filtered by facility)
        var dbLoads = await _context.Loads
            .Where(l => l.FacilityId == facilityId)
            .ToListAsync();

        // 3. Map Infrastructure Models -> Domain Entities
        var domainLoads = dbLoads.Select(MapToDomain).ToList();

        // 4. Call Power Management Service with Domain Entities
        var (activeSource, updatedDomainLoads) = _powerService.CalculateLoadState(CurrentState, domainLoads);

        return Ok(new
        {
            powerState = CurrentState with { ActiveSource = activeSource },
            loads = updatedDomainLoads
        });
    }

    [HttpPost("update-source")]
    public async Task<IActionResult> UpdatePowerSource([FromBody] PowerSourceState request)
    {
        var facilityId = GetFacilityIdFromClaims();

        if (facilityId == null)
        {
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });
        }

        CurrentState = request;

        // 1. Fetch DB Models from Infrastructure (filtered by facility)
        var dbLoads = await _context.Loads
            .Where(l => l.FacilityId == facilityId)
            .ToListAsync();

        // 2. Map Infrastructure Models -> Domain Entities
        var domainLoads = dbLoads.Select(MapToDomain).ToList();

        // 3. Calculate updated load state in Domain
        var (activeSource, updatedDomainLoads) = _powerService.CalculateLoadState(CurrentState, domainLoads);

        // 4. Update DB Models with updated IsActive status from Domain Entities
        foreach (var updatedDomainLoad in updatedDomainLoads)
        {
            var dbLoad = dbLoads.FirstOrDefault(l => l.Id == updatedDomainLoad.Id);
            if (dbLoad != null && dbLoad.IsActive != updatedDomainLoad.IsActive)
            {
                dbLoad.IsActive = updatedDomainLoad.IsActive;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Power state updated successfully.",
            activeSource = activeSource,
            loads = updatedDomainLoads
        });
    }

    /// <summary>
    /// Helper method to map Infrastructure Persistence Model to Domain Entity
    /// </summary>
    private static BlackoutGuard.Domain.Entities.Load MapToDomain(BlackoutGuard.Infrastructure.Persistence.Models.Load dbLoad)
    {
        return new BlackoutGuard.Domain.Entities.Load(
            Id: dbLoad.Id,
            FacilityId: dbLoad.FacilityId,
            ZoneId: dbLoad.ZoneId,
            Name: dbLoad.Name ?? string.Empty,
            RelayAddress: dbLoad.RelayAddress,
            PowerRatingKw: dbLoad.PowerRatingKw,
            Priority: dbLoad.Priority ?? "Low",
            PriorityMode: dbLoad.PriorityMode ?? "auto",
            IsActive: dbLoad.IsActive,
            IsSheddable: dbLoad.IsSheddable,
            SafetyRisk: dbLoad.CriticalityQ1 ?? 1,
            DataLossRisk: dbLoad.CriticalityQ2 ?? 1,
            OperationalRisk: dbLoad.CriticalityQ3 ?? 1,
            ComfortRisk: dbLoad.CriticalityQ4 ?? 1,
            CriticalityScore: dbLoad.CriticalityScore ?? 0
        );
    }
}

/// <summary>
/// Front-end UI မှ POST request ပို့လိုက်သော JSON Payload Body DTO
/// </summary>
public record FacilityConfigUpdateRequest(
    bool GridOnline,
    double SolarCapacityKw,
    double GeneratorCapacityKw
);
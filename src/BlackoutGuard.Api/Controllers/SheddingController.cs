using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.UseCases.Shedding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace BlackoutGuard.Api.Controllers;

[ApiController]
[Route("api/v1/shedding")]
[Authorize]
public class SheddingController : ControllerBase
{
    private const string FacilityIdClaimType = "facility_id";

    private readonly EvaluateSheddingUseCase _evaluateUseCase;
    private readonly ExecuteSheddingUseCase _executeUseCase;

    public SheddingController(
        EvaluateSheddingUseCase evaluateUseCase,
        ExecuteSheddingUseCase executeUseCase)
    {
        _evaluateUseCase = evaluateUseCase;
        _executeUseCase = executeUseCase;
    }

    /// <summary>
    /// Preview or simulate load shedding plan without making database changes.
    /// </summary>
    [HttpPost("evaluate")]
    public async Task<IActionResult> Evaluate([FromQuery] double available_capacity_kw, CancellationToken ct)
    {
        var facilityId = GetFacilityIdFromClaims();
        if (facilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        var result = await _evaluateUseCase.ExecuteAsync(facilityId.Value, available_capacity_kw, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.ErrorMessage });
    }

    /// <summary>
    /// Execute load shedding plan (deactivates target loads & writes audit logs).
    /// </summary>
    [HttpPost("execute")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Execute([FromQuery] double available_capacity_kw, CancellationToken ct)
    {
        var facilityId = GetFacilityIdFromClaims();
        if (facilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        var result = await _executeUseCase.ExecuteAsync(facilityId.Value, available_capacity_kw, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.ErrorMessage });
    }

    private Guid? GetFacilityIdFromClaims()
    {
        var claimValue = User.FindFirstValue(FacilityIdClaimType);
        return Guid.TryParse(claimValue, out var facilityId) ? facilityId : null;
    }
}
using System.Security.Claims;
using BlackoutGuard.Application;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.UseCases.Schedules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlackoutGuard.Api.Controllers;

[ApiController]
[Route("api/v1/schedules")]
[Authorize]
public class SchedulesController : ControllerBase
{
    private const string FacilityIdClaimType = "facility_id";

    private readonly ListSchedulesUseCase _listSchedulesUseCase;
    private readonly CreateScheduleUseCase _createScheduleUseCase;
    private readonly DeleteScheduleUseCase _deleteScheduleUseCase;

    public SchedulesController(
        ListSchedulesUseCase listSchedulesUseCase,
        CreateScheduleUseCase createScheduleUseCase,
        DeleteScheduleUseCase deleteScheduleUseCase)
    {
        _listSchedulesUseCase = listSchedulesUseCase;
        _createScheduleUseCase = createScheduleUseCase;
        _deleteScheduleUseCase = deleteScheduleUseCase;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var facilityId = GetFacilityIdFromClaims();
        if (facilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        var result = await _listSchedulesUseCase.ExecuteAsync(facilityId.Value, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : MapFailure(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateScheduleRequest request, CancellationToken ct)
    {
        var facilityId = GetFacilityIdFromClaims();
        if (facilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        request.FacilityId = facilityId.Value;

        var result = await _createScheduleUseCase.ExecuteAsync(request, ct);

        if (!result.IsSuccess)
            return MapFailure(result);

        return Created($"/api/v1/schedules/{result.Value}", new { id = result.Value });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var facilityId = GetFacilityIdFromClaims();
        if (facilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        var result = await _deleteScheduleUseCase.ExecuteAsync(id, facilityId.Value, ct);

        return result.IsSuccess
            ? NoContent()
            : MapFailure(result);
    }

    private Guid? GetFacilityIdFromClaims()
    {
        var claimValue = User.FindFirstValue(FacilityIdClaimType);
        return Guid.TryParse(claimValue, out var facilityId) ? facilityId : null;
    }

    private IActionResult MapFailure(Result result)
    {
        if (result.ErrorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            return NotFound(new { error = result.ErrorMessage });

        return BadRequest(new { error = result.ErrorMessage });
    }
}

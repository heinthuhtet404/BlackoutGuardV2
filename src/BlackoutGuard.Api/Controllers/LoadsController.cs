using System.Security.Claims;
using BlackoutGuard.Application;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.UseCases.Loads;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlackoutGuard.Api.Controllers;

[ApiController]
[Route("api/v1/loads")]
[Authorize]
public class LoadsController : ControllerBase
{
    private const string FacilityIdClaimType = "facility_id";

    private readonly ListLoadsUseCase _listLoadsUseCase;
    private readonly CreateLoadUseCase _createLoadUseCase;
    private readonly UpdateLoadUseCase _updateLoadUseCase;
    private readonly DeleteLoadUseCase _deleteLoadUseCase;
    private readonly ScoreCriticalityUseCase _scoreCriticalityUseCase;

    public LoadsController(
        ListLoadsUseCase listLoadsUseCase,
        CreateLoadUseCase createLoadUseCase,
        UpdateLoadUseCase updateLoadUseCase,
        DeleteLoadUseCase deleteLoadUseCase,
        ScoreCriticalityUseCase scoreCriticalityUseCase)
    {
        _listLoadsUseCase = listLoadsUseCase;
        _createLoadUseCase = createLoadUseCase;
        _updateLoadUseCase = updateLoadUseCase;
        _deleteLoadUseCase = deleteLoadUseCase;
        _scoreCriticalityUseCase = scoreCriticalityUseCase;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? zone_id, CancellationToken ct)
    {
        var facilityId = GetFacilityIdFromClaims();
        if (facilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        var result = await _listLoadsUseCase.ExecuteAsync(facilityId.Value, zone_id, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : MapFailure(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateLoadRequest request, [FromQuery] bool force = false, CancellationToken ct = default)
    {
        var facilityId = GetFacilityIdFromClaims();
        if (facilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        request.FacilityId = facilityId.Value;
        request.Force = force;

        var result = await _createLoadUseCase.ExecuteAsync(request, ct);

        if (!result.IsSuccess)
            return MapFailure(result);

        var response = new { id = result.Value, warning = force ? "Force override applied; warning logged." : (string?)null };
        return Created($"/api/v1/loads/{result.Value}", response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLoadRequest request, [FromQuery] bool force = false, CancellationToken ct = default)
    {
        var facilityId = GetFacilityIdFromClaims();
        if (facilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        request.LoadId = id;
        request.FacilityId = facilityId.Value;
        request.Force = force;

        var result = await _updateLoadUseCase.ExecuteAsync(request, ct);

        return result.IsSuccess
            ? Ok(new { warning = force ? "Force override applied; warning logged." : (string?)null })
            : MapFailure(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var facilityId = GetFacilityIdFromClaims();
        if (facilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        var result = await _deleteLoadUseCase.ExecuteAsync(id, facilityId.Value, ct);

        return result.IsSuccess
            ? NoContent()
            : MapFailure(result);
    }

    [HttpPost("{id:guid}/criticality")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ScoreCriticality(Guid id, [FromBody] ScoreCriticalityRequest request, CancellationToken ct)
    {
        var facilityId = GetFacilityIdFromClaims();
        if (facilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        request.LoadId = id;
        request.FacilityId = facilityId.Value;

        var result = await _scoreCriticalityUseCase.ExecuteAsync(request, ct);

        return result.IsSuccess
            ? Ok(new { score = result.Value!.Score, priority = result.Value.Priority })
            : MapFailure(result);
    }

    private Guid? GetFacilityIdFromClaims()
    {
        var claimValue = User.FindFirstValue(FacilityIdClaimType);
        return Guid.TryParse(claimValue, out var facilityId) ? facilityId : null;
    }

    private IActionResult MapFailure(Result result)
    {
        var message = result.ErrorMessage ?? string.Empty;

        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(new { error = message });

        if (message.Contains("assigned to", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("capacity exceeded", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { error = message });

        return BadRequest(new { error = message });
    }
}

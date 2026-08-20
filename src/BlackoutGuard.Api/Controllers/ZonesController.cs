using System.Security.Claims;
using BlackoutGuard.Application;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.UseCases.Zones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlackoutGuard.Api.Controllers;

[ApiController]
[Route("api/v1/zones")]
[Authorize]
public class ZonesController : ControllerBase
{
    private const string FacilityIdClaimType = "facility_id";

    private readonly ListZonesUseCase _listZonesUseCase;
    private readonly CreateZoneUseCase _createZoneUseCase;
    private readonly UpdateZoneUseCase _updateZoneUseCase;
    private readonly DeleteZoneUseCase _deleteZoneUseCase;
    private readonly GetZoneUseCase _getZoneUseCase;

    public ZonesController(
        ListZonesUseCase listZonesUseCase,
        CreateZoneUseCase createZoneUseCase,
        UpdateZoneUseCase updateZoneUseCase,
        DeleteZoneUseCase deleteZoneUseCase,
        GetZoneUseCase getZoneUseCase)
    {
        _listZonesUseCase = listZonesUseCase;
        _createZoneUseCase = createZoneUseCase;
        _updateZoneUseCase = updateZoneUseCase;
        _deleteZoneUseCase = deleteZoneUseCase;
        _getZoneUseCase = getZoneUseCase;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var facilityId = GetFacilityIdFromClaims();
        if (facilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        var result = await _listZonesUseCase.ExecuteAsync(facilityId.Value, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : MapFailure(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return BadRequest(new { error = "Zone ID cannot be empty." });

        var facilityId = GetFacilityIdFromClaims();
        if (facilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        var result = await _getZoneUseCase.ExecuteAsync(id, facilityId.Value, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : MapFailure(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateZoneRequest request, CancellationToken ct)
    {
        var facilityId = GetFacilityIdFromClaims();
        if (facilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        var result = await _createZoneUseCase.ExecuteAsync(
            facilityId.Value, request.Name, request.Type, request.ParentZoneId, ct);

        if (!result.IsSuccess)
            return MapFailure(result);

        return Created($"/api/v1/zones/{result.Value}", new { id = result.Value });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateZoneRequest request, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return BadRequest(new { error = "Zone ID cannot be empty." });

        var facilityId = GetFacilityIdFromClaims();
        if (facilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        var result = await _updateZoneUseCase.ExecuteAsync(
            id, facilityId.Value, request.Name, request.Type, request.ParentZoneId, ct);

        return result.IsSuccess
            ? Ok()
            : MapFailure(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return BadRequest(new { error = "Zone ID cannot be empty." });

        var facilityId = GetFacilityIdFromClaims();
        if (facilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        var result = await _deleteZoneUseCase.ExecuteAsync(id, facilityId.Value, ct);

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
using System.Security.Claims;
using BlackoutGuard.Application;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.UseCases.Rules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlackoutGuard.Api.Controllers;

[ApiController]
[Route("api/v1/rules")]
[Authorize]
public class RulesController : ControllerBase
{
    private const string FacilityIdClaimType = "facility_id";

    private readonly ListRulesUseCase _listRulesUseCase;
    private readonly UpdateRuleUseCase _updateRuleUseCase;

    public RulesController(
        ListRulesUseCase listRulesUseCase,
        UpdateRuleUseCase updateRuleUseCase)
    {
        _listRulesUseCase = listRulesUseCase;
        _updateRuleUseCase = updateRuleUseCase;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var facilityId = GetFacilityIdFromClaims();
        if (facilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        var result = await _listRulesUseCase.ExecuteAsync(facilityId.Value, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : MapFailure(result);
    }

    // ⚡ Add Rule အတွက် POST Endpoint (Ok() သို့ ပြောင်းလဲထားပါသည်)
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] UpdateRuleRequest request, CancellationToken ct)
    {
        var facilityId = GetFacilityIdFromClaims();
        if (facilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        if (request.RuleId == Guid.Empty)
        {
            request.RuleId = Guid.NewGuid();
        }
        request.FacilityId = facilityId.Value;

        var result = await _updateRuleUseCase.ExecuteAsync(request, ct);

        return result.IsSuccess
            ? Ok()
            : MapFailure(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRuleRequest request, CancellationToken ct)
    {
        var facilityId = GetFacilityIdFromClaims();
        if (facilityId is null)
            return Unauthorized(new { error = "Missing or invalid facility_id claim." });

        request.RuleId = id;
        request.FacilityId = facilityId.Value;

        var result = await _updateRuleUseCase.ExecuteAsync(request, ct);

        return result.IsSuccess
            ? Ok()
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
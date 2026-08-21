using System;
using System.Threading;
using System.Threading.Tasks;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.UseCases.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlackoutGuard.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly ListUsersUseCase _listUsersUseCase;
    private readonly CreateUserUseCase _createUserUseCase;
    private readonly UpdateUserUseCase _updateUserUseCase;
    private readonly DeleteUserUseCase _deleteUserUseCase;

    public UsersController(
        ListUsersUseCase listUsersUseCase,
        CreateUserUseCase createUserUseCase,
        UpdateUserUseCase updateUserUseCase,
        DeleteUserUseCase deleteUserUseCase)
    {
        _listUsersUseCase = listUsersUseCase;
        _createUserUseCase = createUserUseCase;
        _updateUserUseCase = updateUserUseCase;
        _deleteUserUseCase = deleteUserUseCase;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var result = await _listUsersUseCase.ExecuteAsync(tenantId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.ErrorMessage);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var result = await _createUserUseCase.ExecuteAsync(tenantId, request.Email, request.Password, request.Role, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(List), new { id = result.Value.Id }, result.Value)
                                : BadRequest(result.ErrorMessage);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var currentUserId = GetCurrentUserId();
        var result = await _updateUserUseCase.ExecuteAsync(tenantId, id, request, currentUserId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.ErrorMessage);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var currentUserId = GetCurrentUserId();
        var result = await _deleteUserUseCase.ExecuteAsync(tenantId, id, currentUserId, ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.ErrorMessage);
    }

    private Guid GetTenantId()
    {
        var claim = User.FindFirst("tenant_id") ?? User.FindFirst("tenantId");
        if (claim is null)
            throw new UnauthorizedAccessException("Tenant ID not found in token.");
        return Guid.Parse(claim.Value);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst("user_id") ?? User.FindFirst("userId") ?? User.FindFirst("nameid");
        if (claim is null)
            throw new UnauthorizedAccessException("User ID not found in token.");
        return Guid.Parse(claim.Value);
    }
}
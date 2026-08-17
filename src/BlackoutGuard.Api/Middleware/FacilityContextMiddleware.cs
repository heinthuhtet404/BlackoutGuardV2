using System.Security.Claims;
using BlackoutGuard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlackoutGuard.Api.Middleware;

public class FacilityContextMiddleware
{
    private readonly RequestDelegate _next;

    public FacilityContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, BlackoutGuardDbContext dbContext)
    {
        // SignalR Hub endpoints (/hubs) များကို DB Context locking နှင့် WebSocket handshake နှောင့်နှေးမှု မဖြစ်စေရန် Bypass လုပ်သည်
        if (context.Request.Path.StartsWithSegments("/hubs"))
        {
            await _next(context);
            return;
        }

        // Only enforce facility scoping for authenticated requests.
        // Anonymous endpoints (login, health, swagger) pass through untouched.
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var claimValue = context.User.FindFirstValue("facility_id");
            if (!Guid.TryParse(claimValue, out var facilityId))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    new { error = "Authenticated request is missing a valid facility_id claim." },
                    context.RequestAborted);
                return;
            }

            var connection = dbContext.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(context.RequestAborted);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = $"SET app.current_facility_id = '{facilityId}'";
            await command.ExecuteNonQueryAsync(context.RequestAborted);
        }

        await _next(context);
    }
}
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
        // SignalR Hub endpoints (/hubs) များကို Bypass လုပ်မည်
        if (context.Request.Path.StartsWithSegments("/hubs"))
        {
            await _next(context);
            return;
        }

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

            // PostgreSQL Provider ဖြစ်ပါက Safe SQL Interpolation ဖြင့် RLS Session Variable သတ်မှတ်မည်
            if (dbContext.Database.IsRelational())
            {
                var connection = dbContext.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync(context.RequestAborted);
                }

                await using var command = connection.CreateCommand();
                command.Connection = connection; // Ensure explicit connection association
                command.CommandText = "SELECT set_config('app.current_facility_id', @facilityId, false);";

                var param = command.CreateParameter();
                param.ParameterName = "@facilityId";
                param.Value = facilityId.ToString();
                command.Parameters.Add(param);

                await command.ExecuteNonQueryAsync(context.RequestAborted);
            }
        }

        await _next(context);
    }
}
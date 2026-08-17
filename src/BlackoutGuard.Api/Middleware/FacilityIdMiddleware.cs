using System.Security.Claims;
using BlackoutGuard.Infrastructure.Persistence;

namespace BlackoutGuard.Api.Middleware;

public class FacilityIdMiddleware
{
    private readonly RequestDelegate _next;

    public FacilityIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // SignalR Hub requests များကို Bypass ပြုလုပ်သည်
        if (context.Request.Path.StartsWithSegments("/hubs"))
        {
            await _next(context);
            return;
        }

        var claim = context.User.FindFirstValue("facility_id");
        if (Guid.TryParse(claim, out var facilityId))
        {
            FacilityIdContext.FacilityId = facilityId;
        }

        try
        {
            await _next(context);
        }
        finally
        {
            FacilityIdContext.FacilityId = null;
        }
    }
}
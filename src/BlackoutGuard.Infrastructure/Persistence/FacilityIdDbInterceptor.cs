using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace BlackoutGuard.Infrastructure.Persistence;

public class FacilityIdDbInterceptor : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        var facilityId = FacilityIdContext.GetCurrent();

        if (connection is NpgsqlConnection npgsql)
        {
            if (facilityId is not null)
            {
                await using var setCmd = new NpgsqlCommand(
                    $"SET app.current_facility_id = '{facilityId.Value}'", npgsql);
                await setCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                await using var resetCmd = new NpgsqlCommand(
                    "RESET app.current_facility_id", npgsql);
                await resetCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}

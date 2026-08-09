using Npgsql;

namespace BlackoutGuard.Infrastructure.Persistence;

public static class RlsScriptRunner
{
    public static async Task ApplyAsync(NpgsqlConnection connection)
    {
        var assembly = typeof(RlsScriptRunner).Assembly;
        using var stream = assembly.GetManifestResourceStream("BlackoutGuard.Infrastructure.Persistence.Scripts.001_rls_policies.sql");
        if (stream is null)
            throw new FileNotFoundException("RLS policy script not found as embedded resource.");

        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

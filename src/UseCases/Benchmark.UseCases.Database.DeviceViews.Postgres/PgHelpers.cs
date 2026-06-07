using Npgsql;

namespace Benchmark.UseCases.Database.DeviceViews.Postgres;

/// <summary>Small Npgsql helpers shared by the relational and JSONB device stores.</summary>
internal static class PgHelpers
{
    public static async Task<NpgsqlConnection> OpenAsync(string connStr, CancellationToken ct)
    {
        var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);
        return conn;
    }

    public static async Task ExecAsync(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Table + indexes + TOAST size, in megabytes.</summary>
    public static async Task<double> SizeMbAsync(string connStr, string table, CancellationToken ct)
    {
        await using var conn = await OpenAsync(connStr, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT pg_total_relation_size('{table}')";
        var bytes = (long)(await cmd.ExecuteScalarAsync(ct))!;
        return bytes / 1024.0 / 1024.0;
    }
}

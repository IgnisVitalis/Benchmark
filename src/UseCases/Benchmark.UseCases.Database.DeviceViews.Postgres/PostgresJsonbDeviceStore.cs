using System.Text.Json;
using NpgsqlTypes;

namespace Benchmark.UseCases.Database.DeviceViews.Postgres;

/// <summary>
/// Devices as a single JSONB column ("only JSONB"). The unique key and the type get explicit expression
/// indexes (`(data->>'uuid')` unique, `(data->>'type')`); serial and battery are unindexed, so those
/// queries scan and pay JSONB extraction on every row.
/// </summary>
public sealed class PostgresJsonbDeviceStore(string connStr) : IDeviceStore
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public string Name => "JSONB";

    public async Task ResetAsync(CancellationToken ct)
    {
        await using var conn = await PgHelpers.OpenAsync(connStr, ct);
        await PgHelpers.ExecAsync(conn, """
            DROP TABLE IF EXISTS devices_jsonb;
            CREATE TABLE devices_jsonb (data jsonb NOT NULL);
            CREATE UNIQUE INDEX ux_devices_jsonb_uuid ON devices_jsonb ((data->>'uuid'));
            CREATE INDEX ix_devices_jsonb_type ON devices_jsonb ((data->>'type'));
            """, ct);
    }

    public async Task InsertManyAsync(IReadOnlyList<Device> devices, CancellationToken ct)
    {
        await using var conn = await PgHelpers.OpenAsync(connStr, ct);
        await using var w = await conn.BeginBinaryImportAsync("COPY devices_jsonb (data) FROM STDIN (FORMAT BINARY)", ct);
        foreach (var d in devices)
        {
            await w.StartRowAsync(ct);
            await w.WriteAsync(JsonSerializer.Serialize(d, Json), NpgsqlDbType.Jsonb, ct);
        }
        await w.CompleteAsync(ct);
    }

    public async Task<long> FindByUuidAsync(IReadOnlyList<Guid> uuids, int repeats, CancellationToken ct)
    {
        await using var conn = await PgHelpers.OpenAsync(connStr, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT data->>'uuid' FROM devices_jsonb WHERE data->>'uuid' = @u";   // unique expression index
        var p = cmd.Parameters.Add("u", NpgsqlDbType.Text);
        await cmd.PrepareAsync(ct);

        long found = 0;
        for (int i = 0; i < repeats; i++)
        {
            ct.ThrowIfCancellationRequested();
            p.Value = uuids[i % uuids.Count].ToString();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) found++;
        }
        return found;
    }

    public async Task<long> FindByTypeAsync(IReadOnlyList<string> types, int repeats, int limit, CancellationToken ct)
    {
        await using var conn = await PgHelpers.OpenAsync(connStr, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT data->>'uuid' FROM devices_jsonb WHERE data->>'type' = @t LIMIT {limit}";
        var p = cmd.Parameters.Add("t", NpgsqlDbType.Text);
        await cmd.PrepareAsync(ct);

        long found = 0;
        for (int i = 0; i < repeats; i++)
        {
            ct.ThrowIfCancellationRequested();
            p.Value = types[i % types.Count];
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) found++;
        }
        return found;
    }

    public async Task<long> FindBySerialAsync(IReadOnlyList<string> serials, int repeats, CancellationToken ct)
    {
        await using var conn = await PgHelpers.OpenAsync(connStr, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT data->>'uuid' FROM devices_jsonb WHERE data->>'serial_number' = @s";   // no index → scan + extract
        var p = cmd.Parameters.Add("s", NpgsqlDbType.Text);
        await cmd.PrepareAsync(ct);

        long found = 0;
        for (int i = 0; i < repeats; i++)
        {
            ct.ThrowIfCancellationRequested();
            p.Value = serials[i % serials.Count];
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) found++;
        }
        return found;
    }

    public async Task<long> UpdateLastSeenAsync(IReadOnlyList<Guid> uuids, int repeats, DateTimeOffset lastSeen, CancellationToken ct)
    {
        await using var conn = await PgHelpers.OpenAsync(connStr, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE devices_jsonb SET data = jsonb_set(data, '{last_seen}', to_jsonb(@ts::text)) WHERE data->>'uuid' = @u";
        var pts = cmd.Parameters.Add("ts", NpgsqlDbType.Text);
        var pu  = cmd.Parameters.Add("u",  NpgsqlDbType.Text);
        await cmd.PrepareAsync(ct);

        long affected = 0;
        for (int i = 0; i < repeats; i++)
        {
            ct.ThrowIfCancellationRequested();
            pts.Value = lastSeen.ToString("O");
            pu.Value  = uuids[i % uuids.Count].ToString();
            affected += await cmd.ExecuteNonQueryAsync(ct);
        }
        return affected;
    }

    public async Task<long> RangeByBatteryAsync(int maxBattery, int repeats, int limit, CancellationToken ct)
    {
        await using var conn = await PgHelpers.OpenAsync(connStr, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT data->>'uuid' FROM devices_jsonb WHERE (data->>'battery_level')::int < {maxBattery} LIMIT {limit}";   // no index → scan + cast
        await cmd.PrepareAsync(ct);

        long found = 0;
        for (int i = 0; i < repeats; i++)
        {
            ct.ThrowIfCancellationRequested();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) found++;
        }
        return found;
    }

    public Task<double> StorageSizeMbAsync(CancellationToken ct) => PgHelpers.SizeMbAsync(connStr, "devices_jsonb", ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

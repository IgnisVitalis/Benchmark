using Npgsql;
using NpgsqlTypes;

namespace Benchmark.UseCases.Database.DeviceViews.Postgres;

/// <summary>Devices as a normal table — 20 typed columns, PK on uuid, a non-unique index on type.</summary>
public sealed class PostgresRelationalDeviceStore(string connStr) : IDeviceStore
{
    public string Name => "Relational";

    public async Task ResetAsync(CancellationToken ct)
    {
        await using var conn = await PgHelpers.OpenAsync(connStr, ct);
        await PgHelpers.ExecAsync(conn, """
            DROP TABLE IF EXISTS devices;
            CREATE TABLE devices (
                uuid              uuid PRIMARY KEY,
                name              text NOT NULL,
                type              text NOT NULL,
                manufacturer      text NOT NULL,
                model             text NOT NULL,
                serial_number     text NOT NULL,
                firmware_version  text NOT NULL,
                hardware_revision text NOT NULL,
                mac_address       text NOT NULL,
                ip_address        text NOT NULL,
                location          text NOT NULL,
                latitude          double precision NOT NULL,
                longitude         double precision NOT NULL,
                status            text NOT NULL,
                battery_level     integer NOT NULL,
                signal_strength   integer NOT NULL,
                is_active         boolean NOT NULL,
                last_seen         timestamptz NOT NULL,
                registered_at     timestamptz NOT NULL,
                description       text NOT NULL
            );
            CREATE INDEX ix_devices_type ON devices(type);
            """, ct);
    }

    public async Task InsertManyAsync(IReadOnlyList<Device> devices, CancellationToken ct)
    {
        await using var conn = await PgHelpers.OpenAsync(connStr, ct);
        await using var w = await conn.BeginBinaryImportAsync(
            "COPY devices (uuid,name,type,manufacturer,model,serial_number,firmware_version,hardware_revision," +
            "mac_address,ip_address,location,latitude,longitude,status,battery_level,signal_strength,is_active," +
            "last_seen,registered_at,description) FROM STDIN (FORMAT BINARY)", ct);

        foreach (var d in devices)
        {
            await w.StartRowAsync(ct);
            await w.WriteAsync(d.Uuid,             NpgsqlDbType.Uuid,        ct);
            await w.WriteAsync(d.Name,             NpgsqlDbType.Text,        ct);
            await w.WriteAsync(d.Type,             NpgsqlDbType.Text,        ct);
            await w.WriteAsync(d.Manufacturer,     NpgsqlDbType.Text,        ct);
            await w.WriteAsync(d.Model,            NpgsqlDbType.Text,        ct);
            await w.WriteAsync(d.SerialNumber,     NpgsqlDbType.Text,        ct);
            await w.WriteAsync(d.FirmwareVersion,  NpgsqlDbType.Text,        ct);
            await w.WriteAsync(d.HardwareRevision, NpgsqlDbType.Text,        ct);
            await w.WriteAsync(d.MacAddress,       NpgsqlDbType.Text,        ct);
            await w.WriteAsync(d.IpAddress,        NpgsqlDbType.Text,        ct);
            await w.WriteAsync(d.Location,         NpgsqlDbType.Text,        ct);
            await w.WriteAsync(d.Latitude,         NpgsqlDbType.Double,      ct);
            await w.WriteAsync(d.Longitude,        NpgsqlDbType.Double,      ct);
            await w.WriteAsync(d.Status,           NpgsqlDbType.Text,        ct);
            await w.WriteAsync(d.BatteryLevel,     NpgsqlDbType.Integer,     ct);
            await w.WriteAsync(d.SignalStrength,   NpgsqlDbType.Integer,     ct);
            await w.WriteAsync(d.IsActive,         NpgsqlDbType.Boolean,     ct);
            await w.WriteAsync(d.LastSeen,         NpgsqlDbType.TimestampTz, ct);
            await w.WriteAsync(d.RegisteredAt,     NpgsqlDbType.TimestampTz, ct);
            await w.WriteAsync(d.Description,       NpgsqlDbType.Text,        ct);
        }
        await w.CompleteAsync(ct);
    }

    public async Task<long> FindByUuidAsync(IReadOnlyList<Guid> uuids, int repeats, CancellationToken ct)
    {
        await using var conn = await PgHelpers.OpenAsync(connStr, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT uuid FROM devices WHERE uuid = @u";
        var p = cmd.Parameters.Add("u", NpgsqlDbType.Uuid);
        await cmd.PrepareAsync(ct);

        long found = 0;
        for (int i = 0; i < repeats; i++)
        {
            ct.ThrowIfCancellationRequested();
            p.Value = uuids[i % uuids.Count];
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) found++;
        }
        return found;
    }

    public async Task<long> FindByTypeAsync(IReadOnlyList<string> types, int repeats, int limit, CancellationToken ct)
    {
        await using var conn = await PgHelpers.OpenAsync(connStr, ct);
        await using var cmd = conn.CreateCommand();
        // NB: 'type' is low-cardinality, so with a LIMIT the planner may seq-scan rather than use
        // ix_devices_type. The three representations still run the same query — index *usage* is not asserted.
        cmd.CommandText = $"SELECT uuid FROM devices WHERE type = @t LIMIT {limit}";
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
        cmd.CommandText = "SELECT uuid FROM devices WHERE serial_number = @s";   // no index → seq scan
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
        cmd.CommandText = "UPDATE devices SET last_seen = @ts WHERE uuid = @u";
        var pts = cmd.Parameters.Add("ts", NpgsqlDbType.TimestampTz);
        var pu  = cmd.Parameters.Add("u",  NpgsqlDbType.Uuid);
        await cmd.PrepareAsync(ct);

        long affected = 0;
        for (int i = 0; i < repeats; i++)
        {
            ct.ThrowIfCancellationRequested();
            pts.Value = lastSeen;
            pu.Value  = uuids[i % uuids.Count];
            affected += await cmd.ExecuteNonQueryAsync(ct);
        }
        return affected;
    }

    public async Task<long> RangeByBatteryAsync(int maxBattery, int repeats, int limit, CancellationToken ct)
    {
        await using var conn = await PgHelpers.OpenAsync(connStr, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT uuid FROM devices WHERE battery_level < {maxBattery} LIMIT {limit}";   // no index → scan
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

    public Task<double> StorageSizeMbAsync(CancellationToken ct) => PgHelpers.SizeMbAsync(connStr, "devices", ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

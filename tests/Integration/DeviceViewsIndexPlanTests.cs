using Benchmark.UseCases.Database.DeviceViews;
using Benchmark.UseCases.Database.DeviceViews.Postgres;
using Npgsql;
using Testcontainers.PostgreSql;

/// <summary>
/// Guards the "indexed" label on the model lookup. The earlier low-cardinality `type` lookup made Postgres
/// seq-scan the relational table while the JSONB index was used — a planner artifact that produced a
/// misleading +2000% "win". `model` (~1000 values) is selective, so on a non-trivial table the planner must
/// use the index in both representations. This test fails if a step labelled "indexed" silently scans.
/// </summary>
public class DeviceViewsIndexPlanTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();

    public Task InitializeAsync() => _pg.StartAsync();
    public Task DisposeAsync()    => _pg.DisposeAsync().AsTask();

    [Fact]
    public async Task ModelLookup_OnALoadedTable_UsesTheIndexNotASeqScan()
    {
        var pg    = _pg.GetConnectionString();
        var model = DeviceGenerator.Generate(0).Model;   // a real model value present in the set

        var relational = new PostgresRelationalDeviceStore(pg);
        await relational.ResetAsync(default);
        await relational.InsertManyAsync(DeviceGenerator.Stream(50_000), default);   // big enough that the planner prefers the index

        var jsonb = new PostgresJsonbDeviceStore(pg);
        await jsonb.ResetAsync(default);
        await jsonb.InsertManyAsync(DeviceGenerator.Stream(50_000), default);

        await AssertUsesIndex(pg, $"SELECT uuid FROM devices WHERE model = '{model}' LIMIT 100");
        await AssertUsesIndex(pg, $"SELECT data->>'uuid' FROM devices_jsonb WHERE data->>'model' = '{model}' LIMIT 100");

        // Counter-check: the NON-indexed serial lookup must seq-scan — proving the test can tell them apart.
        await AssertSeqScans(pg, "SELECT uuid FROM devices WHERE serial_number = 'nope' LIMIT 100");

        await relational.DisposeAsync();
        await jsonb.DisposeAsync();
    }

    private static async Task AssertUsesIndex(string connStr, string query)
    {
        var plan = await ExplainAsync(connStr, query);
        Assert.Contains("Index", plan);          // Index Scan / Index Only Scan / Bitmap Index Scan
        Assert.DoesNotContain("Seq Scan", plan);  // not a full-table scan
    }

    private static async Task AssertSeqScans(string connStr, string query) =>
        Assert.Contains("Seq Scan", await ExplainAsync(connStr, query));

    private static async Task<string> ExplainAsync(string connStr, string query)
    {
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("EXPLAIN " + query, conn);

        var plan = new System.Text.StringBuilder();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) plan.AppendLine(r.GetString(0));
        return plan.ToString();
    }
}

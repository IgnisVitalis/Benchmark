using System.Data.Common;

class PointLookupsCase(BenchmarkConfig cfg) : IBenchmarkCase
{
    public int    Number   => 3;
    public string Scenario => $"{cfg.LookupCount:N0} point lookups";

    public async Task<CaseResult?> RunAsync(BenchmarkContext ctx)
    {
        ctx.SampledIds = await ctx.Provider.SampleIdsAsync(ctx.Config.LookupCount);
        await using var conn = await ctx.Provider.OpenConnectionAsync();
        var elapsed = await BenchmarkContext.MeasureAsync(() => LookupAsync(conn, ctx.SampledIds));
        return new(ctx.SampledIds.Length, elapsed);
    }

    private static async Task LookupAsync(DbConnection conn, Guid[] ids)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM benchmark_rows WHERE id = @id";
        var p = cmd.CreateParameter();
        p.ParameterName = "@id";
        p.DbType = System.Data.DbType.Guid;
        p.Value = Guid.Empty;
        cmd.Parameters.Add(p);
        await cmd.PrepareAsync();

        foreach (var id in ids)
        {
            p.Value = id;
            await using var r = await cmd.ExecuteReaderAsync();
            await r.ReadAsync();
        }
    }
}

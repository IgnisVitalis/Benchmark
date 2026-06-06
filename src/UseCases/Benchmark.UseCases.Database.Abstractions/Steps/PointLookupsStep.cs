using System.Data;
using System.Data.Common;
using Benchmark.Core;

namespace Benchmark.UseCases.Database.Steps;

/// <summary>Prepared SELECT-by-primary-key on the populated table. Samples ids first and publishes
/// them to the context for any later step that wants the same keys.</summary>
public sealed class PointLookupsStep(DatabaseConfig cfg) : IBenchmarkStep
{
    public string Name => $"{cfg.LookupCount:N0} point lookups";

    public async Task<StepResult?> RunAsync(UseCaseContext ctx)
    {
        var ids = await ctx.Provider().SampleIdsAsync(cfg.LookupCount);
        ctx.SetSampledIds(ids);

        await using var conn = await ctx.Provider().OpenConnectionAsync();
        var elapsed = await StopwatchEngine.MeasureAsync(() => LookupAsync(conn, ids, ctx.Ct));
        return StepResult.Throughput(ids.Length, elapsed, "lookups/sec");
    }

    private static async Task LookupAsync(DbConnection conn, Guid[] ids, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM benchmark_rows WHERE id = @id";
        var p = cmd.CreateParameter();
        p.ParameterName = "@id";
        p.DbType = DbType.Guid;
        p.Value = Guid.Empty;
        cmd.Parameters.Add(p);
        await cmd.PrepareAsync(ct);

        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            p.Value = id;
            await using var r = await cmd.ExecuteReaderAsync(ct);
            await r.ReadAsync(ct);
        }
    }
}

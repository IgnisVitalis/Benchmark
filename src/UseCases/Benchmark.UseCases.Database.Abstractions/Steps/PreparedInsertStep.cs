using System.Data.Common;
using Benchmark.Core;

namespace Benchmark.UseCases.Database.Steps;

/// <summary>Reuses a single prepared statement across rows; isolates parse/plan overhead.</summary>
public sealed class PreparedInsertStep(DatabaseConfig cfg) : IBenchmarkStep
{
    public string Name => $"+{cfg.SingleCount} rows, prepared, 1 tx/row";

    public async Task<StepResult?> RunAsync(UseCaseContext ctx)
    {
        var rows = InsertHelpers.PrepareRows(cfg.SingleCount, ctx.IdGenerator());
        await using var conn = await ctx.Provider().OpenConnectionAsync();
        await using var cmd  = await ctx.Provider().BuildPreparedInsertCommandAsync(conn, rows[0]);
        var elapsed = await StopwatchEngine.MeasureAsync(() => InsertAsync(conn, cmd, rows, ctx.Ct));
        return StepResult.Throughput(rows.Length, elapsed);
    }

    private static async Task InsertAsync(DbConnection conn, DbCommand cmd, BenchRow[] rows, CancellationToken ct)
    {
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            InsertHelpers.SetInsertValues(cmd, row);
            await using var tx = await conn.BeginTransactionAsync(ct);
            cmd.Transaction = tx;
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
        }
    }
}

using System.Data.Common;
using Benchmark.Core;

namespace Benchmark.UseCases.Database.Steps;

/// <summary>All rows in a single transaction — removes the per-row fsync of one-tx-per-row.</summary>
public sealed class SharedTxInsertStep(DatabaseConfig cfg) : IBenchmarkStep
{
    public string Name => $"+{cfg.SingleCount} rows, 1 tx total";

    public async Task<StepResult?> RunAsync(UseCaseContext ctx)
    {
        var rows = InsertHelpers.PrepareRows(cfg.SingleCount, ctx.IdGenerator());
        await using var conn = await ctx.Provider().OpenConnectionAsync();
        var elapsed = await StopwatchEngine.MeasureAsync(() => InsertAsync(conn, rows, ctx.Ct));
        return StepResult.Throughput(rows.Length, elapsed);
    }

    private static async Task InsertAsync(DbConnection conn, BenchRow[] rows, CancellationToken ct)
    {
        await using var tx = await conn.BeginTransactionAsync(ct);
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = InsertHelpers.InsertSql;
            InsertHelpers.AddInsertParams(cmd, row);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}

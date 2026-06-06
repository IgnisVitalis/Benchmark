using System.Data.Common;
using Benchmark.Core;

namespace Benchmark.UseCases.Database.Steps;

/// <summary>One transaction and one round-trip per row — the baseline for single inserts.</summary>
public sealed class SingleInsertStep(DatabaseConfig cfg) : IBenchmarkStep
{
    public string Name => $"+{cfg.SingleCount} rows, 1 tx/row";

    public async Task<StepResult?> RunAsync(UseCaseContext ctx)
    {
        var rows = InsertHelpers.PrepareRows(cfg.SingleCount, ctx.IdGenerator());
        await using var conn = await ctx.Provider().OpenConnectionAsync();
        var elapsed = await StopwatchEngine.MeasureAsync(() => InsertAsync(conn, rows, ctx.Ct));
        return StepResult.Throughput(rows.Length, elapsed);
    }

    private static async Task InsertAsync(DbConnection conn, BenchRow[] rows, CancellationToken ct)
    {
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            await using var tx  = await conn.BeginTransactionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = InsertHelpers.InsertSql;
            InsertHelpers.AddInsertParams(cmd, row);
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
        }
    }
}

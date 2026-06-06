using System.Data.Common;
using Benchmark.Core;

namespace Benchmark.UseCases.Database.Steps;

/// <summary>Single INSERT with every row in one VALUES clause and one round-trip.</summary>
public sealed class BatchedInsertStep(DatabaseConfig cfg) : IBenchmarkStep
{
    public string Name => $"+{cfg.SingleCount} rows, batched VALUES";

    public async Task<StepResult?> RunAsync(UseCaseContext ctx)
    {
        var rows = InsertHelpers.PrepareRows(cfg.SingleCount, ctx.IdGenerator());
        await using var conn = await ctx.Provider().OpenConnectionAsync();
        var elapsed = await StopwatchEngine.MeasureAsync(() => InsertAsync(conn, rows));
        return StepResult.Throughput(rows.Length, elapsed);
    }

    private static async Task InsertAsync(DbConnection conn, BenchRow[] rows)
    {
        await using var tx  = await conn.BeginTransactionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = InsertHelpers.BuildBatchedSql(rows.Length);
        for (int i = 0; i < rows.Length; i++)
            InsertHelpers.AddInsertParams(cmd, rows[i], suffix: $"{i}");
        await cmd.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }
}

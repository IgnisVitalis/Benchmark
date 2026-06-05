using System.Data.Common;

class BatchedInsertCase(BenchmarkConfig cfg) : IBenchmarkCase
{
    public int    Number   => 7;
    public string Scenario => $"+{cfg.SingleCount} rows, batched VALUES";

    public async Task<CaseResult?> RunAsync(BenchmarkContext ctx)
    {
        var rows = InsertHelpers.PrepareRows(ctx.Config.SingleCount, ctx.NewId);
        await using var conn = await ctx.Provider.OpenConnectionAsync();
        var elapsed = await BenchmarkContext.MeasureAsync(() => InsertAsync(conn, rows));
        return new(rows.Length, elapsed);
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

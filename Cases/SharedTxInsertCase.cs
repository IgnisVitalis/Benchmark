using System.Data.Common;

class SharedTxInsertCase(BenchmarkConfig cfg) : IBenchmarkCase
{
    public int    Number   => 6;
    public string Scenario => $"+{cfg.SingleCount} rows, 1 tx total";

    public async Task<CaseResult?> RunAsync(BenchmarkContext ctx)
    {
        var rows = InsertHelpers.PrepareRows(ctx.Config.SingleCount, ctx.NewId);
        await using var conn = await ctx.Provider.OpenConnectionAsync();
        var elapsed = await BenchmarkContext.MeasureAsync(() => InsertAsync(conn, rows));
        return new(rows.Length, elapsed);
    }

    private static async Task InsertAsync(DbConnection conn, BenchRow[] rows)
    {
        await using var tx = await conn.BeginTransactionAsync();
        foreach (var row in rows)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = InsertHelpers.InsertSql;
            InsertHelpers.AddInsertParams(cmd, row);
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }
}

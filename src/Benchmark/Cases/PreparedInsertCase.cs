using System.Data.Common;

class PreparedInsertCase(BenchmarkConfig cfg) : IBenchmarkCase
{
    public int    Number   => 5;
    public string Scenario => $"+{cfg.SingleCount} rows, prepared, 1 tx/row";

    public async Task<CaseResult?> RunAsync(BenchmarkContext ctx)
    {
        var rows = InsertHelpers.PrepareRows(ctx.Config.SingleCount, ctx.NewId);
        await using var conn = await ctx.Provider.OpenConnectionAsync();
        await using var cmd  = InsertHelpers.BuildInsertCommand(conn, rows[0]);
        await cmd.PrepareAsync();
        var elapsed = await BenchmarkContext.MeasureAsync(() => InsertAsync(conn, cmd, rows));
        return new(rows.Length, elapsed);
    }

    private static async Task InsertAsync(DbConnection conn, DbCommand cmd, BenchRow[] rows)
    {
        foreach (var row in rows)
        {
            InsertHelpers.SetInsertValues(cmd, row);
            await using var tx = await conn.BeginTransactionAsync();
            cmd.Transaction = tx;
            await cmd.ExecuteNonQueryAsync();
            await tx.CommitAsync();
        }
    }
}

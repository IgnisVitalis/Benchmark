class BulkInsertCase(BenchmarkConfig cfg) : IBenchmarkCase
{
    public int    Number   => 1;
    public string Scenario => $"Bulk insert {cfg.BulkCount:N0} rows";

    public async Task<CaseResult?> RunAsync(BenchmarkContext ctx)
    {
        var rows = InsertHelpers.PrepareRows(ctx.Config.BulkCount, ctx.NewId);
        await ctx.Provider.TruncateAsync();
        var elapsed = await BenchmarkContext.MeasureAsync(() => ctx.Provider.BulkInsertAsync(rows));
        return new(ctx.Config.BulkCount, elapsed);
    }
}

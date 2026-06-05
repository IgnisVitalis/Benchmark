class IndexSizeCase : IBenchmarkCase
{
    public int    Number   => 2;
    public string Scenario => "Index size after bulk insert";

    public async Task<CaseResult?> RunAsync(BenchmarkContext ctx)
    {
        var size = await ctx.Provider.GetIndexSizeAsync();
        ctx.Write($"  {Number,-2} {Scenario,-45} {size,20}");
        return null;
    }
}

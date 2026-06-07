using Benchmark.Core;

namespace Benchmark.UseCases.Database.Steps;

/// <summary>Loads N rows through the provider's native bulk protocol; first step in the chain — it
/// populates the table the later read steps depend on.</summary>
public sealed class BulkInsertStep(DatabaseConfig cfg) : IBenchmarkStep
{
    public string Name => $"Bulk insert {NumberFormat.Value(cfg.BulkCount)} rows";

    public async Task<StepResult?> RunAsync(UseCaseContext ctx)
    {
        var rows = InsertHelpers.PrepareRows(cfg.BulkCount, ctx.IdGenerator());
        await ctx.Provider().TruncateAsync();
        var elapsed = await StopwatchEngine.MeasureAsync(() => ctx.Provider().BulkInsertAsync(rows));
        return StepResult.Throughput(cfg.BulkCount, elapsed);
    }
}

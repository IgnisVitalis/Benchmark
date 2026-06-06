using Benchmark.Core;

namespace Benchmark.UseCases.Database.Steps;

/// <summary>Reports the primary-key index size after the bulk load — shows page fragmentation from
/// random vs ordered keys. Lower is better, and it is comparable across variants.</summary>
public sealed class IndexSizeStep : IBenchmarkStep
{
    public string Name => "Index size after bulk insert";

    public async Task<StepResult?> RunAsync(UseCaseContext ctx)
    {
        double mb = await ctx.Provider().GetIndexSizeAsync();
        return new StepResult("index size", mb, "MB", IsLowerBetter: true);
    }
}

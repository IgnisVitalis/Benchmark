namespace Benchmark.Core;

/// <summary>Classifies a step by its metric so the console and Markdown reporters group rows the same way:
/// a rate (unit ends in "/sec") → throughput, a recorded time → time, anything else → size/other.</summary>
internal static class MetricGroups
{
    public static StepStats? First(StepRow r) => r.Cells.FirstOrDefault(c => c is not null);

    public static bool IsRate(StepRow r)  => First(r)?.Unit.EndsWith("/sec", StringComparison.OrdinalIgnoreCase) == true;
    public static bool IsTimed(StepRow r) => First(r)?.MedianElapsed is not null;
    public static bool IsOther(StepRow r) => !IsRate(r) && !IsTimed(r);
}

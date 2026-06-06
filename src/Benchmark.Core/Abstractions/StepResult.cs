namespace Benchmark.Core;

/// <summary>
/// The outcome of a single benchmark step. <see cref="Value"/> is the comparable number used to
/// compute gain across variants; everything else is for display. A step may return <c>null</c>
/// from <see cref="IBenchmarkStep.RunAsync"/> to be omitted from the report entirely.
/// </summary>
/// <param name="Metric">What was measured, e.g. "throughput", "index size".</param>
/// <param name="Value">The comparable figure. Higher is better unless <paramref name="IsLowerBetter"/>.</param>
/// <param name="Unit">Unit of <paramref name="Value"/>, e.g. "rows/sec", "MB", "ms".</param>
/// <param name="Count">Optional row/operation count behind the measurement.</param>
/// <param name="Elapsed">Optional wall-clock time, shown as an "ms" annotation.</param>
/// <param name="IsLowerBetter">When true, a lower <paramref name="Value"/> is the improvement.</param>
public sealed record StepResult(
    string     Metric,
    double     Value,
    string     Unit,
    long?      Count        = null,
    TimeSpan?  Elapsed      = null,
    bool       IsLowerBetter = false)
{
    /// <summary>Builds a throughput result (Value = count / seconds, Unit = "ops/sec").</summary>
    public static StepResult Throughput(long count, TimeSpan elapsed, string unit = "rows/sec") =>
        new(unit, count / Math.Max(elapsed.TotalSeconds, 1e-9), unit, count, elapsed);
}

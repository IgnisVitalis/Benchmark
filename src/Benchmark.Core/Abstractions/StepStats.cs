namespace Benchmark.Core;

/// <summary>
/// Aggregate of several <see cref="StepResult"/> samples for one (step, variant) cell. The
/// <see cref="Median"/> is the representative figure used for display and gain; <see cref="Cv"/>
/// (coefficient of variation) conveys run-to-run noise.
/// </summary>
public sealed record StepStats(
    string    Unit,
    double    Median,
    double    Mean,
    double    StdDev,
    double    Min,
    double    Max,
    int       Samples,
    bool      IsLowerBetter,
    TimeSpan? MedianElapsed)
{
    /// <summary>Coefficient of variation (StdDev / Mean) — 0 when there is a single sample.</summary>
    public double Cv => Mean == 0 ? 0 : StdDev / Mean;

    /// <summary>Aggregates the per-iteration samples of one step (all share unit + direction).</summary>
    public static StepStats From(IReadOnlyList<StepResult> samples)
    {
        var first  = samples[0];
        var values = samples.Select(s => s.Value).OrderBy(x => x).ToArray();
        double mean = values.Average();

        var elapsed = samples.Where(s => s.Elapsed.HasValue).Select(s => s.Elapsed!.Value).ToList();

        return new StepStats(
            Unit:          first.Unit,
            Median:        MedianOf(values),
            Mean:          mean,
            StdDev:        StdDevOf(values, mean),
            Min:           values[0],
            Max:           values[^1],
            Samples:       values.Length,
            IsLowerBetter: first.IsLowerBetter,
            MedianElapsed: elapsed.Count > 0 ? MedianTime(elapsed) : null);
    }

    private static double MedianOf(double[] sorted) =>
        sorted.Length == 0 ? 0
        : sorted.Length % 2 == 1 ? sorted[sorted.Length / 2]
        : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2.0;

    private static double StdDevOf(double[] values, double mean) =>
        values.Length < 2 ? 0 : Math.Sqrt(values.Sum(x => (x - mean) * (x - mean)) / (values.Length - 1));

    private static TimeSpan MedianTime(List<TimeSpan> times)
    {
        var t = times.Select(x => x.Ticks).OrderBy(x => x).ToArray();
        long m = t.Length % 2 == 1 ? t[t.Length / 2] : (t[t.Length / 2 - 1] + t[t.Length / 2]) / 2;
        return TimeSpan.FromTicks(m);
    }
}

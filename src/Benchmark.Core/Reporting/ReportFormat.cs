using System.Globalization;

namespace Benchmark.Core;

/// <summary>
/// Shared, culture-invariant cell + gain formatting so the console table and the Markdown report render
/// identical text. Cells show the median, the run-to-run spread (±CV%) when there is more than one
/// sample, and the median wall-clock time.
/// </summary>
public static class ReportFormat
{
    /// <summary>e.g. "384,313 rows/sec ±2.1% (2,602 ms)"; "—" when absent.</summary>
    public static string Cell(StepStats? s)
    {
        if (s is null) return "—";
        string value  = $"{NumberFormat.Value(s.Median)} {s.Unit}";
        string spread = s.Samples > 1 && s.Mean != 0 ? $" ±{Pct(s.Cv * 100)}%" : "";
        string ms     = s.MedianElapsed is { } e ? $" ({NumberFormat.Value(e.TotalMilliseconds)} ms)" : "";
        return value + spread + ms;
    }

    /// <summary>Signed % change of the median vs the baseline cell, flipped so "+" always means "improved".</summary>
    public static string Gain(StepStats? current, StepStats? baseline)
    {
        if (current is null || baseline is null || baseline.Median == 0) return "—";
        double pct = (current.Median - baseline.Median) * 100.0 / baseline.Median;
        if (current.IsLowerBetter) pct = -pct;
        return $"{(pct >= 0 ? "+" : "")}{Pct(pct)}%";
    }

    private static string Pct(double v) => v.ToString("F1", CultureInfo.InvariantCulture);
}

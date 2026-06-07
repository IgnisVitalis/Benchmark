using Benchmark.Core;

/// <summary>The compact report must split a mixed-metric run into Throughput / Time / Difference / Size,
/// keep size metrics out of the % table, and show them as a × multiplier.</summary>
public class CompactReporterTests
{
    private static StepStats Rate(double perSec, double ms) =>
        StepStats.From([new StepResult("rows/sec", perSec, "rows/sec", Count: 100, Elapsed: TimeSpan.FromMilliseconds(ms))]);

    private static StepStats Size(double mb) =>
        StepStats.From([new StepResult("size", mb, "MB", IsLowerBetter: true)]);

    private static UseCaseReport Report() => new(
        new UseCaseMetadata("t", "T", "C", BenchmarkEngine.Stopwatch, "d"),
        EnvironmentInfo.Capture(),
        ["A", "B"],
        [
            new StepRow("Load",    [Rate(1000, 100), Rate(1200, 83)]),
            new StepRow("Storage", [Size(50), Size(75)]),
        ],
        Warmup: 1, Iterations: 3);

    [Fact]
    public void Render_MixedMetrics_SplitsIntoThroughputTimeDeltaAndSize()
    {
        var md = CompactReporter.Render(Report());

        Assert.Contains("## Throughput", md);
        Assert.Contains("## Time", md);
        Assert.Contains("## Difference vs baseline", md);
        Assert.Contains("## Size", md);
    }

    [Fact]
    public void Render_RateStep_ShowsGainVsBaseline()
    {
        var md = CompactReporter.Render(Report());

        Assert.Contains("+20.0%", md);   // B (1,200) vs A (1,000) throughput
    }

    [Fact]
    public void Render_SizeStep_IsAMultiplierAndOutsideTheDeltaTable()
    {
        var md = CompactReporter.Render(Report());

        Assert.Contains("(1.5×)", md);   // 75 MB / 50 MB
        var deltaSection = md[md.IndexOf("## Difference", StringComparison.Ordinal)..md.IndexOf("## Size", StringComparison.Ordinal)];
        Assert.DoesNotContain("Storage", deltaSection);
    }
}

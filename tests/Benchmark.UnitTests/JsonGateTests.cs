using Benchmark.Core;

/// <summary>
/// Docker-free coverage of the Tier-2 validation pieces: JSON round-trips of the report model and the
/// regression gate's direction/threshold logic.
/// </summary>
public class JsonGateTests
{
    private static StepStats Stat(string unit, double v, bool lowerBetter = false) =>
        StepStats.From([new StepResult(unit, v, unit, IsLowerBetter: lowerBetter)]);

    private static UseCaseReport Sample(double bulkValue) => new(
        new UseCaseMetadata("test.case", "Test", "Database", BenchmarkEngine.Stopwatch, "desc"),
        EnvironmentInfo.Capture(),
        ["A", "B"],
        [
            new StepRow("Bulk insert", [Stat("rows/sec", bulkValue), Stat("rows/sec", bulkValue * 1.2)]),
            new StepRow("Index size",  [Stat("MB", 100, lowerBetter: true), Stat("MB", 100, lowerBetter: true)]),
        ],
        Warmup: 1, Iterations: 3);

    [Fact]
    public void JsonReporter_RoundTrip_PreservesReportModel()
    {
        var report = Sample(1000);

        var back = JsonReporter.Read(JsonReporter.Render(report));

        Assert.Equal(report.Metadata.Id, back.Metadata.Id);
        Assert.Equal(report.Variants, back.Variants);
        Assert.Equal(report.Rows.Count, back.Rows.Count);
        Assert.Equal(report.Iterations, back.Iterations);
        Assert.Equal(report.Rows[0].Cells[0]!.Median, back.Rows[0].Cells[0]!.Median);
        Assert.True(back.Rows[1].Cells[0]!.IsLowerBetter);
    }

    [Fact]
    public void Compare_ThroughputDropBeyondThreshold_FlagsRegression()
    {
        var result = RegressionGate.Compare(Sample(800), Sample(1000), thresholdPct: 10);   // -20%

        Assert.True(result.HasRegressions);
        Assert.Contains(result.Regressions, r => r.Step == "Bulk insert" && r.Variant == "A" && r.ImprovePct < -10);
    }

    [Fact]
    public void Compare_ChangeWithinThreshold_ReportsNoRegression()
    {
        var result = RegressionGate.Compare(Sample(950), Sample(1000), thresholdPct: 10);    // -5%

        Assert.False(result.HasRegressions);
    }

    [Fact]
    public void Compare_WorseLowerIsBetterMetric_FlagsRegression()
    {
        var baseline = Sample(1000);
        var worse = baseline with
        {
            Rows =
            [
                baseline.Rows[0],
                new StepRow("Index size", [Stat("MB", 130, lowerBetter: true), Stat("MB", 130, lowerBetter: true)]),
            ],
        };

        var result = RegressionGate.Compare(worse, baseline, thresholdPct: 10);   // 100 → 130 MB

        Assert.Contains(result.Regressions, r => r.Step == "Index size");
    }
}

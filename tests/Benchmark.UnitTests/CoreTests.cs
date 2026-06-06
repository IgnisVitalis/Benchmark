using Benchmark.Core;

/// <summary>Unit coverage of the pure Core logic: number formatting, stats aggregation, table rendering,
/// and the use-case registry.</summary>
public class CoreTests
{
    // ── NumberFormat ──────────────────────────────────────────────────────────
    [Theory]
    [InlineData(0,        "0")]
    [InlineData(299133,   "299,133")]
    [InlineData(382,      "382")]
    [InlineData(5.42,     "5.42")]
    [InlineData(0.305,    "0.305")]
    [InlineData(-12.5,    "-12.5")]
    public void NumberFormat_is_adaptive_and_invariant(double v, string expected) =>
        Assert.Equal(expected, NumberFormat.Value(v));

    // ── StepStats ─────────────────────────────────────────────────────────────
    [Fact]
    public void StepStats_aggregates_median_min_max_samples()
    {
        var s = StepStats.From(
        [
            new StepResult("rows/sec", 100, "rows/sec"),
            new StepResult("rows/sec", 300, "rows/sec"),
            new StepResult("rows/sec", 200, "rows/sec"),
        ]);

        Assert.Equal(200, s.Median);
        Assert.Equal(200, s.Mean);
        Assert.Equal(100, s.Min);
        Assert.Equal(300, s.Max);
        Assert.Equal(3,   s.Samples);
        Assert.True(s.StdDev > 0);
    }

    [Fact]
    public void StepStats_single_sample_has_zero_spread()
    {
        var s = StepStats.From([new StepResult("MB", 42, "MB", IsLowerBetter: true)]);

        Assert.Equal(42, s.Median);
        Assert.Equal(0,  s.StdDev);
        Assert.Equal(0,  s.Cv);
        Assert.True(s.IsLowerBetter);
    }

    [Fact]
    public void StepStats_even_count_medians_the_two_middle()
    {
        var s = StepStats.From(
        [
            new StepResult("u", 10, "u"), new StepResult("u", 20, "u"),
            new StepResult("u", 30, "u"), new StepResult("u", 40, "u"),
        ]);

        Assert.Equal(25, s.Median);   // (20 + 30) / 2
    }

    // ── ConsoleReporter.RenderTable ───────────────────────────────────────────
    [Fact]
    public void RenderTable_adds_a_delta_column_and_computes_gain()
    {
        var report = new UseCaseReport(
            new UseCaseMetadata("t", "T", "C", BenchmarkEngine.Stopwatch, "d"),
            EnvironmentInfo.Capture(),
            ["A", "B"],
            [new StepRow("step", [Stat(100), Stat(150)])],
            Warmup: 0, Iterations: 1);

        var lines  = ConsoleReporter.RenderTable(report);
        var header = lines[1];

        Assert.Contains("A",  header);
        Assert.Contains("B",  header);
        Assert.Contains("Δ%", header);
        Assert.Contains(lines, l => l.Contains("+50.0%"));   // 100 → 150
    }

    // ── UseCaseRegistry ───────────────────────────────────────────────────────
    [Fact]
    public void Registry_resolves_case_insensitively_and_filters_by_category()
    {
        var reg = new UseCaseRegistry()
            .Register(() => new FakeUseCase("a.one", "Cat"))
            .Register(() => new FakeUseCase("a.two", "Cat"));

        Assert.Equal(2, reg.Catalog.Count);
        Assert.NotNull(reg.Resolve("A.ONE"));
        Assert.Null(reg.Resolve("missing"));
        Assert.Equal(2, reg.ResolveCategory("cat").Count);
    }

    private static StepStats Stat(double v) => StepStats.From([new StepResult("u", v, "u")]);

    private sealed class FakeUseCase(string id, string category) : IUseCase
    {
        public UseCaseMetadata Metadata { get; } = new(id, id, category, BenchmarkEngine.Stopwatch, "d");
        public Task<UseCaseReport> RunAsync(HostContext host) =>
            Task.FromResult(new UseCaseReport(Metadata, EnvironmentInfo.Capture(), ["x"], []));
    }
}

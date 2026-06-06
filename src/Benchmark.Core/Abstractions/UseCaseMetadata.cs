namespace Benchmark.Core;

/// <summary>How a use case is timed.</summary>
public enum BenchmarkEngine
{
    /// <summary>BCL Stopwatch harness (chained steps + variants). Good for DB / integration work.</summary>
    Stopwatch,

    /// <summary>BenchmarkDotNet, for statistically rigorous in-memory micro-benchmarks.</summary>
    BenchmarkDotNet,
}

/// <summary>
/// Identity and description of a use case. Maps 1:1 to one generated report under <c>Results/</c>.
/// </summary>
/// <param name="Id">Stable, lowercase, dotted id, e.g. "database.postgres.uuid-insert". Becomes the file name.</param>
/// <param name="Title">Human-readable title shown in reports.</param>
/// <param name="Category">Top-level grouping, e.g. "Database", "Algorithms".</param>
/// <param name="Engine">Which timing engine drives it.</param>
/// <param name="Description">One or two sentences describing what the use case exercises.</param>
public sealed record UseCaseMetadata(
    string          Id,
    string          Title,
    string          Category,
    BenchmarkEngine Engine,
    string          Description)
{
    /// <summary>Relative path of the generated report, e.g. "Results/database.postgres.uuid-insert.md".</summary>
    public string ResultsFile => $"Results/{Id}.md";
}

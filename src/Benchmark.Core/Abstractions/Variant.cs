namespace Benchmark.Core;

/// <summary>
/// One labelled run of the whole step chain. <see cref="Apply"/> mutates the freshly created context
/// for that run (e.g. swaps the data generator). The first variant in a use case is the baseline;
/// gain for every other variant is reported relative to it.
/// </summary>
public sealed record Variant(string Label, Action<UseCaseContext> Apply)
{
    /// <summary>A single, no-op variant — for use cases that have nothing to compare against.</summary>
    public static readonly Variant Single = new("default", static _ => { });
}

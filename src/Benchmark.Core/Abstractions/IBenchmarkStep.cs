namespace Benchmark.Core;

/// <summary>
/// The smallest measured unit (e.g. "bulk insert", "point lookups"). Steps run in order, sharing one
/// <see cref="UseCaseContext"/> — that shared context is the chain: an earlier step can publish state
/// (a populated table, sampled ids) that a later step consumes.
/// </summary>
public interface IBenchmarkStep
{
    /// <summary>Human-readable label shown as the row name in reports.</summary>
    string Name { get; }

    /// <summary>Runs the step. Return <c>null</c> to omit this step from the report matrix.</summary>
    Task<StepResult?> RunAsync(UseCaseContext ctx);
}

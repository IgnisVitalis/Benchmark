namespace Benchmark.Core;

/// <summary>
/// A named, runnable benchmark bundle. Produces exactly one <see cref="UseCaseReport"/>
/// (which the host renders to <c>Results/&lt;id&gt;.md</c>). Implement this directly for a custom
/// engine, or derive from <see cref="ChainedUseCase"/> for the standard ordered-steps + variants model.
/// </summary>
public interface IUseCase
{
    UseCaseMetadata Metadata { get; }

    Task<UseCaseReport> RunAsync(HostContext host);
}

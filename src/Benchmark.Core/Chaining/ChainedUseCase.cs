namespace Benchmark.Core;

/// <summary>
/// Base class for Stopwatch-engine use cases. A subclass supplies the ordered <see cref="Steps"/>,
/// the <see cref="Variants"/> to compare, an optional one-time <see cref="SetupAsync"/> /
/// <see cref="TeardownAsync"/> (e.g. create/drop a database) and <see cref="SeedContext"/> to publish
/// shared resources (a provider, config) into every variant's context.
/// </summary>
public abstract class ChainedUseCase : IUseCase
{
    public abstract UseCaseMetadata Metadata { get; }

    protected abstract IReadOnlyList<IBenchmarkStep> Steps    { get; }
    protected abstract IReadOnlyList<Variant>        Variants { get; }

    /// <summary>Publishes constant resources (provider, config, ...) into each variant's context.</summary>
    protected virtual void SeedContext(UseCaseContext ctx) { }

    /// <summary>Runs once before any variant — e.g. create the database and table.</summary>
    protected virtual Task SetupAsync(HostContext host) => Task.CompletedTask;

    /// <summary>Runs once after all variants — e.g. drop the database. Always runs, even on failure.</summary>
    protected virtual Task TeardownAsync(HostContext host) => Task.CompletedTask;

    public async Task<UseCaseReport> RunAsync(HostContext host)
    {
        await SetupAsync(host);
        try
        {
            return await new ChainRunner(host.Log)
                .RunAsync(Metadata, Steps, Variants, SeedContext, host.Iterations, host.Warmup, host.Ct);
        }
        finally
        {
            await TeardownAsync(host);
        }
    }
}

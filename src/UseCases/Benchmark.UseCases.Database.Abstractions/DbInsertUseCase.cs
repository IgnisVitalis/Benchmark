using Benchmark.Core;
using Benchmark.UseCases.Database.Steps;

namespace Benchmark.UseCases.Database;

/// <summary>
/// The reusable UUID insert/read use case, independent of any specific engine. It chains the seven
/// steps and runs them under two variants — random <c>Guid.NewGuid()</c> (baseline) and time-ordered
/// <c>Guid v7</c> — so every step reports its v7 gain. A concrete engine subclass only supplies the
/// <see cref="IDbProvider"/> and the <see cref="UseCaseMetadata"/>.
/// </summary>
public abstract class DbInsertUseCase(DatabaseConfig cfg) : ChainedUseCase
{
    private IDbProvider? _provider;

    protected DatabaseConfig Config => cfg;

    /// <summary>Builds the engine-specific provider (this is where the driver dependency enters).</summary>
    protected abstract IDbProvider CreateProvider();

    protected override IReadOnlyList<IBenchmarkStep> Steps =>
    [
        new BulkInsertStep(cfg),
        new IndexSizeStep(),
        new PointLookupsStep(cfg),
        new SingleInsertStep(cfg),
        new PreparedInsertStep(cfg),
        new SharedTxInsertStep(cfg),
        new BatchedInsertStep(cfg),
    ];

    protected override IReadOnlyList<Variant> Variants =>
    [
        new("Guid.NewGuid()", static c => c.Set<Func<Guid>>(Guid.NewGuid)),
        new("Guid v7",        static c => c.Set<Func<Guid>>(Guid.CreateVersion7)),
    ];

    protected override void SeedContext(UseCaseContext ctx)
    {
        ctx.Set(_provider!);
        ctx.Set(cfg);
    }

    protected override async Task SetupAsync(HostContext host)
    {
        _provider = CreateProvider();
        await _provider.CreateDatabaseAsync();
        host.Log.Line("Database 'Benchmark' created.");
        await _provider.SetupTableAsync();
        host.Log.Line("Table ready.");
        host.Log.Line("");
    }

    protected override async Task TeardownAsync(HostContext host)
    {
        if (_provider is null) return;
        await _provider.DropDatabaseAsync();
        host.Log.Line("Database 'Benchmark' dropped.");
        await _provider.DisposeAsync();
        _provider = null;
    }
}

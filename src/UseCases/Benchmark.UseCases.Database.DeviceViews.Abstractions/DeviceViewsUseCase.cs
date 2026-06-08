using Benchmark.Core;
using Benchmark.UseCases.Database.DeviceViews.Steps;

namespace Benchmark.UseCases.Database.DeviceViews;

/// <summary>
/// Compares the same Device collection stored several ways (relational columns, JSONB, …). The
/// representations are passed in as <see cref="NamedStore"/>s — so the host decides which to compare —
/// and each becomes a variant column in one report. The dataset and lookup keys are generated once and
/// reused across every variant, so the representation is the only thing that differs.
/// </summary>
public sealed class DeviceViewsUseCase(DeviceViewsConfig cfg, IReadOnlyList<NamedStore> stores) : ChainedUseCase
{
    private LookupKeys _keys = new([], [], []);

    public override UseCaseMetadata Metadata { get; } = new(
        Id:          "database.device-views",
        Title:       "Device storage — relational vs JSONB vs MongoDB",
        Category:    "Database",
        Engine:      BenchmarkEngine.Stopwatch,
        Description: "The same 20-field Device stored as typed relational columns, a single JSONB column, or "
                   + "MongoDB documents. Compares load, lookups (indexed-unique, indexed, non-indexed), update, "
                   + "a range query and storage size — with identical data and identical indexed fields in every "
                   + "variant (the host chooses which representations to include).");

    protected override IReadOnlyList<IBenchmarkStep> Steps =>
    [
        new LoadStep(cfg),
        new LookupByUuidStep(cfg),
        new LookupByModelStep(cfg),
        new LookupBySerialStep(cfg),
        new UpdateByUuidStep(cfg),
        new RangeByBatteryStep(cfg),
        new StorageSizeStep(),
    ];

    protected override IReadOnlyList<Variant> Variants =>
        stores.Select(s => new Variant(s.Label, ctx => ctx.Set(s.Store))).ToList();

    protected override void SeedContext(UseCaseContext ctx) => ctx.Set(_keys);

    protected override Task SetupAsync(HostContext host)
    {
        _keys = SampleKeys(cfg);
        host.Log.Line($"Streaming {NumberFormat.Value(cfg.DeviceCount)} devices; comparing {stores.Count} representation(s).");
        host.Log.Line("");
        return Task.CompletedTask;
    }

    protected override async Task TeardownAsync(HostContext host)
    {
        foreach (var s in stores)
            await s.Store.DisposeAsync();
    }

    // Sample keys by drawing random indices and generating just those devices — works the same whether the
    // set is 100k or 100M, since each device is reproducible from its index alone.
    private static LookupKeys SampleKeys(DeviceViewsConfig cfg)
    {
        var rng     = new Random(7);
        int sample  = Math.Max(1, Math.Min(cfg.DeviceCount, Math.Max(cfg.LookupCount, cfg.ScanLookupCount)));
        var uuids   = new Guid[sample];
        var serials = new string[sample];
        var models  = new HashSet<string>();
        for (int i = 0; i < sample; i++)
        {
            var d = DeviceGenerator.Generate(rng.Next(cfg.DeviceCount));
            uuids[i]   = d.Uuid;
            serials[i] = d.SerialNumber;
            models.Add(d.Model);
        }
        return new LookupKeys(uuids, models.Count > 0 ? models.ToArray() : ["M0000"], serials);
    }
}

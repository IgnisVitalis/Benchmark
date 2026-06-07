using Benchmark.Core;

namespace Benchmark.UseCases.Database.DeviceViews.Steps;

/// <summary>Typed accessors for the items the use case publishes into the shared context.</summary>
internal static class DeviceContext
{
    public static IDeviceStore Store(this UseCaseContext c)   => c.Get<IDeviceStore>();
    public static Device[]     Devices(this UseCaseContext c) => c.Get<Device[]>();
    public static LookupKeys   Keys(this UseCaseContext c)    => c.Get<LookupKeys>();
}

/// <summary>Resets the store and bulk-loads the (identical, deterministic) dataset.</summary>
public sealed class LoadStep(DeviceViewsConfig cfg) : IBenchmarkStep
{
    public string Name => $"Load {cfg.DeviceCount:N0} devices";

    public async Task<StepResult?> RunAsync(UseCaseContext ctx)
    {
        var store   = ctx.Store();
        var devices = ctx.Devices();
        await store.ResetAsync(ctx.Ct);
        var elapsed = await StopwatchEngine.MeasureAsync(() => store.InsertManyAsync(devices, ctx.Ct));
        return StepResult.Throughput(devices.Length, elapsed, "devices/sec");
    }
}

/// <summary>Point lookup on the indexed unique key (the fast path).</summary>
public sealed class LookupByUuidStep(DeviceViewsConfig cfg) : IBenchmarkStep
{
    public string Name => $"Lookup by uuid — indexed unique ×{cfg.LookupCount:N0}";

    public async Task<StepResult?> RunAsync(UseCaseContext ctx)
    {
        var elapsed = await StopwatchEngine.MeasureAsync(() => ctx.Store().FindByUuidAsync(ctx.Keys().Uuids, cfg.LookupCount, ctx.Ct));
        return StepResult.Throughput(cfg.LookupCount, elapsed, "lookups/sec");
    }
}

/// <summary>Lookup on the indexed non-unique key (index range scan returning a set).</summary>
public sealed class LookupByTypeStep(DeviceViewsConfig cfg) : IBenchmarkStep
{
    public string Name => $"Lookup by type — indexed ×{cfg.LookupCount:N0}";

    public async Task<StepResult?> RunAsync(UseCaseContext ctx)
    {
        var elapsed = await StopwatchEngine.MeasureAsync(() => ctx.Store().FindByTypeAsync(ctx.Keys().Types, cfg.LookupCount, cfg.RangeLimit, ctx.Ct));
        return StepResult.Throughput(cfg.LookupCount, elapsed, "lookups/sec");
    }
}

/// <summary>Point lookup on a NON-indexed field — every op is a sequential scan.</summary>
public sealed class LookupBySerialStep(DeviceViewsConfig cfg) : IBenchmarkStep
{
    public string Name => $"Lookup by serial — NOT indexed ×{cfg.ScanLookupCount:N0}";

    public async Task<StepResult?> RunAsync(UseCaseContext ctx)
    {
        var elapsed = await StopwatchEngine.MeasureAsync(() => ctx.Store().FindBySerialAsync(ctx.Keys().Serials, cfg.ScanLookupCount, ctx.Ct));
        return StepResult.Throughput(cfg.ScanLookupCount, elapsed, "lookups/sec");
    }
}

/// <summary>Update one indexed-key row per op.</summary>
public sealed class UpdateByUuidStep(DeviceViewsConfig cfg) : IBenchmarkStep
{
    public string Name => $"Update by uuid — indexed ×{cfg.LookupCount:N0}";

    public async Task<StepResult?> RunAsync(UseCaseContext ctx)
    {
        var ts = DateTimeOffset.UtcNow;
        var elapsed = await StopwatchEngine.MeasureAsync(() => ctx.Store().UpdateLastSeenAsync(ctx.Keys().Uuids, cfg.LookupCount, ts, ctx.Ct));
        return StepResult.Throughput(cfg.LookupCount, elapsed, "updates/sec");
    }
}

/// <summary>Range/filter query on a NON-indexed numeric field.</summary>
public sealed class RangeByBatteryStep(DeviceViewsConfig cfg) : IBenchmarkStep
{
    public string Name => $"Range battery<{cfg.LowBatteryBelow} — NOT indexed ×{cfg.ScanLookupCount:N0}";

    public async Task<StepResult?> RunAsync(UseCaseContext ctx)
    {
        var elapsed = await StopwatchEngine.MeasureAsync(() => ctx.Store().RangeByBatteryAsync(cfg.LowBatteryBelow, cfg.ScanLookupCount, cfg.RangeLimit, ctx.Ct));
        return StepResult.Throughput(cfg.ScanLookupCount, elapsed, "queries/sec");
    }
}

/// <summary>Total on-disk size of the table + its indexes (lower is better).</summary>
public sealed class StorageSizeStep : IBenchmarkStep
{
    public string Name => "Storage size (table + indexes)";

    public async Task<StepResult?> RunAsync(UseCaseContext ctx)
    {
        double mb = await ctx.Store().StorageSizeMbAsync(ctx.Ct);
        return new StepResult("storage", mb, "MB", IsLowerBetter: true);
    }
}

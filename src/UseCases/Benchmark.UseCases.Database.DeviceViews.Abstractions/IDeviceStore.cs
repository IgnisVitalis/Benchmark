namespace Benchmark.UseCases.Database.DeviceViews;

/// <summary>
/// One storage representation of the device collection (relational table, JSONB column, document DB, …).
/// Each method runs a whole workload (loop of <c>repeats</c> ops) so connection/prepare overhead is
/// amortised and the timing reflects the query, matching the existing DB benchmark style.
/// Implementations live in per-engine leaf projects with their own driver dependency.
/// </summary>
public interface IDeviceStore : IAsyncDisposable
{
    string Name { get; }

    /// <summary>DROP + CREATE the backing table/collection and its indexes.</summary>
    Task ResetAsync(CancellationToken ct);

    Task InsertManyAsync(IReadOnlyList<Device> devices, CancellationToken ct);

    /// <summary>Point lookup on the indexed unique key. Returns total rows matched.</summary>
    Task<long> FindByUuidAsync(IReadOnlyList<Guid> uuids, int repeats, CancellationToken ct);

    /// <summary>Lookup on the indexed non-unique key (returns a bounded set per query).</summary>
    Task<long> FindByTypeAsync(IReadOnlyList<string> types, int repeats, int limit, CancellationToken ct);

    /// <summary>Point lookup on a NON-indexed field — forces a sequential scan.</summary>
    Task<long> FindBySerialAsync(IReadOnlyList<string> serials, int repeats, CancellationToken ct);

    /// <summary>Update one indexed-key row per op. Returns total rows affected.</summary>
    Task<long> UpdateLastSeenAsync(IReadOnlyList<Guid> uuids, int repeats, DateTimeOffset lastSeen, CancellationToken ct);

    /// <summary>Range/filter query on a NON-indexed numeric field (bounded set per query).</summary>
    Task<long> RangeByBatteryAsync(int maxBattery, int repeats, int limit, CancellationToken ct);

    /// <summary>Total on-disk size of the table + its indexes, in megabytes.</summary>
    Task<double> StorageSizeMbAsync(CancellationToken ct);
}

/// <summary>A store paired with the variant label it appears under in the report.</summary>
public sealed record NamedStore(string Label, IDeviceStore Store);

/// <summary>Lookup keys sampled once from the dataset and reused across every variant (fairness).</summary>
public sealed record LookupKeys(IReadOnlyList<Guid> Uuids, IReadOnlyList<string> Types, IReadOnlyList<string> Serials);

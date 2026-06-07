namespace Benchmark.UseCases.Database.DeviceViews;

/// <summary>Configuration for the device-views use case (bound from the "DeviceViews" appsettings section).</summary>
public sealed class DeviceViewsConfig
{
    public int    DeviceCount     { get; set; } = 200_000;
    public int    LookupCount     { get; set; } = 1_000;   // ops for indexed lookups / updates
    public int    ScanLookupCount { get; set; } = 50;      // ops for non-indexed lookups (each is O(N))
    public int    RangeLimit      { get; set; } = 100;     // LIMIT for set-returning queries
    public int    LowBatteryBelow { get; set; } = 20;      // threshold for the range/filter query
    public string ConnStr         { get; set; } = "";      // PostgreSQL (relational + JSONB)
    public string MongoConnStr    { get; set; } = "";      // MongoDB (optional 3rd representation)
}

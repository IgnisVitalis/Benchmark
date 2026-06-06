namespace Benchmark.UseCases.Database;

/// <summary>
/// Configuration for the database use cases. Bound by the host from the "Database" section of
/// appsettings.json. The count fields drive the steps; <see cref="Providers"/> tells the host which
/// engines to register and with what connection strings.
/// </summary>
public sealed class DatabaseConfig
{
    public int BulkCount   { get; set; } = 10_000_000;
    public int SingleCount { get; set; } = 100;
    public int LookupCount { get; set; } = 1_000;

    public List<ProviderConfig> Providers { get; set; } = [];
}

public sealed class ProviderConfig
{
    /// <summary>"PostgreSQL" or "MSSQL".</summary>
    public string Type         { get; set; } = "";
    public bool   Enabled      { get; set; } = true;
    public string ConnStr      { get; set; } = "";
    public string AdminConnStr { get; set; } = "";
}

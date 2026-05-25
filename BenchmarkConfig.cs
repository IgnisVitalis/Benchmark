class BenchmarkConfig
{
    public int    BulkCount     { get; set; } = 10_000_000;
    public int    SingleCount   { get; set; } = 100;
    public int    LookupCount   { get; set; } = 1_000;
public List<ProviderConfig> Providers { get; set; } = [];
}

class ProviderConfig
{
    public string Type         { get; set; } = "";
    public bool   Enabled      { get; set; } = true;
    public string ConnStr      { get; set; } = "";
    public string AdminConnStr { get; set; } = "";
}

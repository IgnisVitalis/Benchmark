using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class BenchmarkRunner(BenchmarkConfig cfg, ILogger? logger = null)
{
    private readonly ILogger _logger = logger ?? NullLogger.Instance;

    private List<IBenchmarkCase> BuildCases() =>
    [
        new BulkInsertCase(cfg),
        new IndexSizeCase(),
        new PointLookupsCase(cfg),
        new SingleInsertCase(cfg),
        new PreparedInsertCase(cfg),
        new SharedTxInsertCase(cfg),
        new BatchedInsertCase(cfg),
    ];

    public async Task RunAsync(IDbProvider provider)
    {
        await provider.CreateDatabaseAsync();
        _logger.LogInformation("Database 'Benchmark' created.");
        _logger.LogInformation("");

        await provider.SetupTableAsync();
        _logger.LogInformation("Table ready.");
        _logger.LogInformation("");

        PrintHeader();

        _logger.LogInformation("--- {Name} / Guid.NewGuid() ---", provider.Name);
        var baselines = await RunRoundAsync(provider, Guid.NewGuid, null);

        _logger.LogInformation("");
        _logger.LogInformation("--- {Name} / Guid v7 ---", provider.Name);
        await RunRoundAsync(provider, Guid.CreateVersion7, baselines);

        _logger.LogInformation("");
        await provider.DropDatabaseAsync();
        _logger.LogInformation("Database 'Benchmark' dropped.");
    }

    private async Task<long[]> RunRoundAsync(IDbProvider provider, Func<Guid> newId, long[]? bl)
    {
        var ctx      = new BenchmarkContext { Provider = provider, Config = cfg, NewId = newId, Logger = _logger };
        var timedRps = new List<long>();
        int blIdx    = 0;

        foreach (var c in BuildCases())
        {
            var result = await c.RunAsync(ctx);
            if (result is not null)
            {
                long baseline = bl?[blIdx] ?? 0;
                timedRps.Add(PrintResult(c.Number, c.Scenario, result.RowCount, result.Elapsed, baseline));
                blIdx++;
            }
        }

        return timedRps.ToArray();
    }

    private void PrintHeader()
    {
        _logger.LogInformation("{Line}", $"  {"#",-2} {"Scenario",-45} {"Ms",8} {"Rows/sec",12}");
        _logger.LogInformation("{Line}", new string('-', 70));
    }

    private long PrintResult(int num, string scenario, long count, TimeSpan elapsed, long baseline = 0)
    {
        long rps = (long)(count / elapsed.TotalSeconds);
        string gain = baseline > 0
            ? $"  {(rps - baseline) * 100.0 / baseline,+6:F1}%"
            : "";
        _logger.LogInformation("{Line}", $"  {num,-2} {scenario,-45} {elapsed.TotalMilliseconds,8:F0} {rps,12:N0}{gain}");
        return rps;
    }
}

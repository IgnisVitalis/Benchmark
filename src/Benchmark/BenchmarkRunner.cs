public class BenchmarkRunner(BenchmarkConfig cfg, Action<string>? write = null)
{
    private readonly Action<string> _write = write ?? Console.WriteLine;

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
        _write("Database 'Benchmark' created.");
        _write("");

        await provider.SetupTableAsync();
        _write("Table ready.");
        _write("");

        PrintHeader();

        _write($"--- {provider.Name} / Guid.NewGuid() ---");
        var baselines = await RunRoundAsync(provider, Guid.NewGuid, null);

        _write("");
        _write($"--- {provider.Name} / Guid v7 ---");
        await RunRoundAsync(provider, Guid.CreateVersion7, baselines);

        _write("");
        await provider.DropDatabaseAsync();
        _write("Database 'Benchmark' dropped.");
    }

    private async Task<long[]> RunRoundAsync(IDbProvider provider, Func<Guid> newId, long[]? bl)
    {
        var ctx      = new BenchmarkContext { Provider = provider, Config = cfg, NewId = newId, Write = _write };
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
        _write($"  {"#",-2} {"Scenario",-45} {"Ms",8} {"Rows/sec",12}");
        _write(new string('-', 70));
    }

    private long PrintResult(int num, string scenario, long count, TimeSpan elapsed, long baseline = 0)
    {
        long rps = (long)(count / elapsed.TotalSeconds);
        string gain = baseline > 0
            ? $"  {(rps - baseline) * 100.0 / baseline,+6:F1}%"
            : "";
        _write($"  {num,-2} {scenario,-45} {elapsed.TotalMilliseconds,8:F0} {rps,12:N0}{gain}");
        return rps;
    }
}

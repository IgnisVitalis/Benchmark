interface IBenchmarkCase
{
    int    Number   { get; }
    string Scenario { get; }

    // null = info-only (case handles its own console output)
    Task<CaseResult?> RunAsync(BenchmarkContext ctx);
}

record CaseResult(long RowCount, TimeSpan Elapsed);

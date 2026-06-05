using System.Diagnostics;

class BenchmarkContext
{
    public required IDbProvider     Provider { get; init; }
    public required BenchmarkConfig Config   { get; init; }
    public required Func<Guid>      NewId    { get; init; }
    public Action<string>           Write    { get; init; } = Console.WriteLine;

    public Guid[] SampledIds { get; set; } = [];

    public static async Task<TimeSpan> MeasureAsync(Func<Task> action)
    {
        var sw = Stopwatch.StartNew();
        await action();
        return sw.Elapsed;
    }
}

using System.Diagnostics;

namespace Benchmark.Core;

/// <summary>BCL-only timing used by chained (DB / integration) steps.</summary>
public static class StopwatchEngine
{
    /// <summary>Settles the GC, then awaits <paramref name="action"/> and returns how long it took.
    /// The forced collection keeps a prior step's garbage from being charged to this measurement.</summary>
    public static async Task<TimeSpan> MeasureAsync(Func<Task> action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var sw = Stopwatch.StartNew();
        await action();
        return sw.Elapsed;
    }
}

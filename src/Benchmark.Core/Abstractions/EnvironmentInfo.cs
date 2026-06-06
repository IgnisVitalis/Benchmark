using System.Runtime.InteropServices;

namespace Benchmark.Core;

/// <summary>Snapshot of where/when a benchmark ran, embedded in every generated report for reproducibility.</summary>
public sealed record EnvironmentInfo(
    DateTimeOffset TimestampUtc,
    string         Machine,
    string         Os,
    string         Runtime,
    int            ProcessorCount)
{
    public static EnvironmentInfo Capture() => new(
        DateTimeOffset.UtcNow,
        Environment.MachineName,
        RuntimeInformation.OSDescription,
        RuntimeInformation.FrameworkDescription,
        Environment.ProcessorCount);
}

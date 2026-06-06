namespace Benchmark.Core;

/// <summary>
/// Minimal, BCL-only logging seam. The framework deliberately does NOT depend on
/// Microsoft.Extensions.Logging so that <c>Benchmark.Core</c> stays dependency-free.
/// Hosts (CLI, tests) provide an implementation.
/// </summary>
public interface IRunLog
{
    void Line(string text);
}

/// <summary>Discards all output.</summary>
public sealed class NullRunLog : IRunLog
{
    public static readonly NullRunLog Instance = new();
    public void Line(string text) { }
}

/// <summary>Writes each line to <see cref="Console"/>.</summary>
public sealed class ConsoleRunLog : IRunLog
{
    public void Line(string text) => Console.WriteLine(text);
}

/// <summary>Forwards each line to an arbitrary sink (e.g. xUnit's ITestOutputHelper.WriteLine).</summary>
public sealed class DelegateRunLog(Action<string> sink) : IRunLog
{
    public void Line(string text) => sink(text);
}

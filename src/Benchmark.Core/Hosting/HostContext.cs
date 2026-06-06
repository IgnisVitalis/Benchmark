namespace Benchmark.Core;

/// <summary>
/// Ambient services a host (CLI, tests) hands to a use case at run time. <see cref="Iterations"/> and
/// <see cref="Warmup"/> control measurement rigor: the chain runs <c>Warmup + Iterations</c> times and
/// only the last <see cref="Iterations"/> passes are recorded and aggregated.
/// </summary>
public sealed class HostContext(IRunLog log, CancellationToken ct = default, int iterations = 1, int warmup = 0)
{
    public IRunLog           Log        { get; } = log;
    public CancellationToken Ct         { get; } = ct;
    public int               Iterations { get; } = Math.Max(1, iterations);
    public int               Warmup     { get; } = Math.Max(0, warmup);
}

/// <summary>
/// Iteration settings for the integration benchmarks: <b>3 measured runs (+1 warm-up) by default</b>, so
/// each reported figure is the median of 3. Override per run via environment variables, e.g.
/// <c>BENCHMARK_ITERATIONS=2 dotnet test</c> or <c>BENCHMARK_WARMUP=0</c>.
/// </summary>
internal static class TestRun
{
    public static int Iterations => EnvInt("BENCHMARK_ITERATIONS", 3);
    public static int Warmup     => EnvInt("BENCHMARK_WARMUP", 1);

    private static int EnvInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v >= 0 ? v : fallback;
}

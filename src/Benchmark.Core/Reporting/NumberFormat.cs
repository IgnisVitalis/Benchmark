using System.Globalization;

namespace Benchmark.Core;

/// <summary>
/// Adaptive, culture-invariant numeric formatting so both large throughput figures and tiny timings
/// stay readable — and so generated reports are identical regardless of the machine's locale.
/// </summary>
public static class NumberFormat
{
    public static string Value(double v) => Math.Abs(v) switch
    {
        0       => "0",
        >= 1000 => v.ToString("N0",    CultureInfo.InvariantCulture),  // 299,133
        >= 1    => v.ToString("0.##",  CultureInfo.InvariantCulture),  // 5.42 , 382
        _       => v.ToString("0.###", CultureInfo.InvariantCulture),  // 0.305
    };
}

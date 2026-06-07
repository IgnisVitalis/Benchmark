using System.Globalization;

namespace Benchmark.Core;

/// <summary>
/// Adaptive, culture-invariant numeric formatting so large throughput figures and tiny timings both stay
/// readable — and generated reports are identical regardless of the machine's locale. Thousands are grouped
/// with a space (e.g. "299 133"), which is cleaner than commas and the international convention.
/// </summary>
public static class NumberFormat
{
    private static readonly NumberFormatInfo Format = new()
    {
        NumberGroupSeparator   = " ",
        NumberDecimalSeparator = ".",
    };

    // From ten upward, decimals are just noise (the run-to-run spread dwarfs them), so round to whole.
    // Decimals survive only where they carry weight: the × multipliers and sub-ten values.
    public static string Value(double v) => Math.Abs(v) switch
    {
        0       => "0",
        >= 1000 => v.ToString("N0",    Format),  // 299 133
        >= 10   => v.ToString("0",     Format),  // 100 , 26 , 596
        >= 1    => v.ToString("0.##",  Format),  // 2.38  (e.g. × multipliers)
        _       => v.ToString("0.###", Format),  // 0.305
    };
}

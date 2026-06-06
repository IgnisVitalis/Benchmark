namespace Benchmark.Core;

/// <summary>One step×variant comparison between a current run and a baseline.</summary>
/// <param name="ImprovePct">Signed % change where positive always means "better" (flipped for
/// lower-is-better metrics). A value below <c>-threshold</c> is a regression.</param>
public sealed record RegressionRow(
    string Step,
    string Variant,
    double BaselineMedian,
    double CurrentMedian,
    double ImprovePct,
    bool   IsRegression);

/// <summary>The outcome of gating a run against a baseline.</summary>
public sealed record RegressionReport(string Id, double ThresholdPct, IReadOnlyList<RegressionRow> Rows)
{
    public IReadOnlyList<RegressionRow> Regressions => Rows.Where(r => r.IsRegression).ToList();
    public bool HasRegressions => Rows.Any(r => r.IsRegression);
}

/// <summary>
/// Compares a fresh <see cref="UseCaseReport"/> against a committed baseline, matching cells by
/// (step name, variant label) and flagging any whose median moved in the wrong direction by more than
/// <c>thresholdPct</c> percent. Pure logic — no I/O.
/// </summary>
public static class RegressionGate
{
    public static RegressionReport Compare(UseCaseReport current, UseCaseReport baseline, double thresholdPct)
    {
        var baseByKey = new Dictionary<(string Step, string Variant), StepStats>();
        for (int r = 0; r < baseline.Rows.Count; r++)
            for (int v = 0; v < baseline.Variants.Count; v++)
                if (baseline.Rows[r].Cells[v] is { } cell)
                    baseByKey[(baseline.Rows[r].Step, baseline.Variants[v])] = cell;

        var rows = new List<RegressionRow>();
        for (int r = 0; r < current.Rows.Count; r++)
        {
            for (int v = 0; v < current.Variants.Count; v++)
            {
                var cur = current.Rows[r].Cells[v];
                if (cur is null) continue;

                var key = (current.Rows[r].Step, current.Variants[v]);
                if (!baseByKey.TryGetValue(key, out var bas) || bas.Median == 0) continue;

                double rawPct     = (cur.Median - bas.Median) * 100.0 / bas.Median;
                double improvePct = cur.IsLowerBetter ? -rawPct : rawPct;   // positive = better
                rows.Add(new RegressionRow(key.Item1, key.Item2, bas.Median, cur.Median, improvePct,
                                           IsRegression: improvePct < -thresholdPct));
            }
        }

        return new RegressionReport(current.Metadata.Id, thresholdPct, rows);
    }
}

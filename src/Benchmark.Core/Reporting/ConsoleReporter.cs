namespace Benchmark.Core;

/// <summary>
/// Console output for a run: terse live progress (one line per warm-up / measured pass) while the
/// chain runs, and a final bordered table that mirrors the Markdown report (variants side by side,
/// median ± CV%, with a Δ% column per non-baseline variant).
/// </summary>
public static class ConsoleReporter
{
    public static void VariantHeader(IRunLog log, string variant, int warmup, int iterations) =>
        log.Line($"  {variant}  ({warmup} warmup + {iterations} measured):");

    /// <summary>One progress line per chain pass, showing how long the whole pass took.</summary>
    public static void IterationDone(IRunLog log, int pass, int warmup, int iterations, TimeSpan elapsed)
    {
        string label = pass < warmup ? $"warmup {pass + 1}/{warmup}" : $"run {pass - warmup + 1}/{iterations}";
        log.Line($"    {label,-12} … {elapsed.TotalSeconds,6:F1}s");
    }

    /// <summary>Writes the consolidated, bordered results table — the same shape as the .md report.</summary>
    public static void Print(IRunLog log, UseCaseReport report)
    {
        foreach (var line in RenderTable(report))
            log.Line(line);
        if (report.Iterations > 1)
            log.Line($"  values = median of {report.Iterations} runs; ± = coefficient of variation");
    }

    /// <summary>Renders the report as an aligned ASCII table (Step | variant… | Δ%…).</summary>
    public static IReadOnlyList<string> RenderTable(UseCaseReport report)
    {
        // Columns: Step | v0 | v1 | Δ% | v2 | Δ% | …  (Δ% only for non-baseline variants)
        var headers = new List<string> { "Step" };
        var right   = new List<bool>   { false };
        for (int v = 0; v < report.Variants.Count; v++)
        {
            headers.Add(report.Variants[v]); right.Add(true);
            if (v > 0) { headers.Add("Δ%"); right.Add(true); }
        }

        var rows = new List<string[]>(report.Rows.Count);
        foreach (var row in report.Rows)
        {
            var cells = new List<string> { row.Step };
            for (int v = 0; v < report.Variants.Count; v++)
            {
                cells.Add(ReportFormat.Cell(row.Cells[v]));
                if (v > 0) cells.Add(ReportFormat.Gain(row.Cells[v], row.Cells[0]));
            }
            rows.Add([.. cells]);
        }

        int cols = headers.Count;
        var w = new int[cols];
        for (int c = 0; c < cols; c++) w[c] = headers[c].Length;
        foreach (var r in rows)
            for (int c = 0; c < cols; c++) w[c] = Math.Max(w[c], r[c].Length);

        string sep = "+" + string.Join("+", w.Select(x => new string('-', x + 2))) + "+";
        string Fmt(IReadOnlyList<string> cells) =>
            "| " + string.Join(" | ", cells.Select((s, c) => right[c] ? s.PadLeft(w[c]) : s.PadRight(w[c]))) + " |";

        var lines = new List<string> { sep, Fmt(headers), sep };
        lines.AddRange(rows.Select(Fmt));
        lines.Add(sep);
        return lines;
    }
}

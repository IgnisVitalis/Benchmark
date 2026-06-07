using System.Text;

namespace Benchmark.Core;

/// <summary>
/// Console output for a run: terse live progress while the chain runs, then the same narrow split tables
/// as the compact report — Throughput, Time, Difference (% vs baseline) and Size — rendered as bordered
/// ASCII tables. The throughput table keeps ±CV% because the console is the live view where noise matters.
/// </summary>
public static class ConsoleReporter
{
    public static void VariantHeader(IRunLog log, string variant, int warmup, int iterations) =>
        log.Line($"  {variant}  ({warmup} warmup + {iterations} measured):");

    public static void IterationDone(IRunLog log, int pass, int warmup, int iterations, TimeSpan elapsed)
    {
        string label = pass < warmup ? $"warmup {pass + 1}/{warmup}" : $"run {pass - warmup + 1}/{iterations}";
        log.Line($"    {label,-12} … {elapsed.TotalSeconds,6:F1}s");
    }

    /// <summary>Prints the consolidated split tables (Throughput · Time · Difference · Size).</summary>
    public static void Print(IRunLog log, UseCaseReport report)
    {
        foreach (var line in Render(report))
            log.Line(line);
    }

    /// <summary>Renders all console lines for the report (testable; <see cref="Print"/> just logs them).</summary>
    public static IReadOnlyList<string> Render(UseCaseReport report)
    {
        var vars  = report.Variants;
        var lines = new List<string>();

        var rate  = report.Rows.Where(MetricGroups.IsRate).ToList();
        var timed = report.Rows.Where(MetricGroups.IsTimed).ToList();
        var speed = report.Rows.Where(r => MetricGroups.IsRate(r) || MetricGroups.IsTimed(r)).ToList();
        var other = report.Rows.Where(MetricGroups.IsOther).ToList();

        if (rate.Count > 0)
            Section(lines, "Throughput — items/sec (higher = faster)", vars, rate,
                    c => c is null ? "—" : NumberFormat.Value(c.Median));

        if (timed.Count > 0)
            Section(lines, "Time — ms (lower = faster)", vars, timed,
                    c => c?.MedianElapsed is { } el ? NumberFormat.Value(el.TotalMilliseconds) : "—");

        if (speed.Count > 0 && vars.Count > 1)
            DeltaSection(lines, "Difference vs baseline — % (+ = faster)", vars, speed);

        if (other.Count > 0)
        {
            lines.Add("");
            lines.Add("Size");
            foreach (var row in other) lines.Add("  " + SizeLine(row, vars));
        }

        if (report.Iterations > 1)
        {
            lines.Add("");
            lines.Add($"  values = median of {report.Iterations} runs (full ±CV% in Results/{report.Metadata.Id}.md)");
        }
        return lines;
    }

    // ── sections ──────────────────────────────────────────────────────────────

    private static void Section(List<string> lines, string title, IReadOnlyList<string> vars,
                                List<StepRow> rows, Func<StepStats?, string> cell)
    {
        var headers = new List<string> { "Step" };
        headers.AddRange(vars);

        var body = rows.Select(r =>
        {
            var cells = new string[headers.Count];
            cells[0] = r.Step;
            for (int v = 0; v < vars.Count; v++) cells[v + 1] = cell(r.Cells[v]);
            return cells;
        }).ToList();

        Emit(lines, title, headers, body);
    }

    private static void DeltaSection(List<string> lines, string title, IReadOnlyList<string> vars, List<StepRow> rows)
    {
        var headers = new List<string> { "Step" };
        for (int v = 1; v < vars.Count; v++) headers.Add(vars[v]);   // baseline omitted (it's the reference)

        var body = rows.Select(r =>
        {
            var cells = new string[headers.Count];
            cells[0] = r.Step;
            for (int v = 1; v < vars.Count; v++) cells[v] = ReportFormat.Gain(r.Cells[v], r.Cells[0]);
            return cells;
        }).ToList();

        Emit(lines, title, headers, body);
    }

    // Bordered table: step name left-aligned, numbers right-aligned.
    private static void Emit(List<string> lines, string title, IReadOnlyList<string> headers, List<string[]> body)
    {
        int cols = headers.Count;
        var w = new int[cols];
        for (int c = 0; c < cols; c++) w[c] = headers[c].Length;
        foreach (var r in body)
            for (int c = 0; c < cols; c++) w[c] = Math.Max(w[c], (r[c] ?? "").Length);

        string sep = "+" + string.Join("+", w.Select(x => new string('-', x + 2))) + "+";
        string Fmt(IReadOnlyList<string> cells) =>
            "| " + string.Join(" | ", cells.Select((s, c) => c == 0 ? (s ?? "").PadRight(w[c]) : (s ?? "").PadLeft(w[c]))) + " |";

        lines.Add("");
        lines.Add(title);
        lines.Add(sep);
        lines.Add(Fmt(headers));
        lines.Add(sep);
        lines.AddRange(body.Select(Fmt));
        lines.Add(sep);
    }

    private static string SizeLine(StepRow row, IReadOnlyList<string> vars)
    {
        var sb    = new StringBuilder(row.Step).Append(": ");
        var base_ = row.Cells.Count > 0 ? row.Cells[0] : null;
        for (int v = 0; v < vars.Count; v++)
        {
            if (v > 0) sb.Append(" · ");
            sb.Append(vars[v]).Append(' ');
            var c = row.Cells[v];
            if (c is null) { sb.Append('—'); continue; }
            sb.Append(NumberFormat.Value(c.Median)).Append(' ').Append(c.Unit);
            if (v > 0 && base_ is not null && base_.Median != 0)
                sb.Append(" (").Append(NumberFormat.Value(c.Median / base_.Median)).Append("×)");
        }
        return sb.ToString();
    }
}

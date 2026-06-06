using System.Diagnostics;

namespace Benchmark.Core;

/// <summary>
/// Executes a chain of steps under each variant, repeating the whole chain <c>Warmup + Iterations</c>
/// times (warm-up passes discarded). Each recorded pass uses a fresh <see cref="UseCaseContext"/>, so
/// state never leaks between passes; the per-pass <see cref="StepResult"/>s are aggregated into a
/// <see cref="StepStats"/> per (step, variant). Repeating the whole chain — rather than each step —
/// means stateful steps (e.g. inserts) always start from the same freshly-rebuilt table.
/// </summary>
public sealed class ChainRunner(IRunLog log)
{
    public async Task<UseCaseReport> RunAsync(
        UseCaseMetadata               meta,
        IReadOnlyList<IBenchmarkStep> steps,
        IReadOnlyList<Variant>        variants,
        Action<UseCaseContext>?       seedContext = null,
        int                           iterations  = 1,
        int                           warmup      = 0,
        CancellationToken             ct          = default)
    {
        iterations = Math.Max(1, iterations);
        warmup     = Math.Max(0, warmup);

        // samples[step][variant] = recorded results across iterations
        var samples = new List<StepResult>[steps.Count][];
        for (int s = 0; s < steps.Count; s++)
        {
            samples[s] = new List<StepResult>[variants.Count];
            for (int v = 0; v < variants.Count; v++)
                samples[s][v] = [];
        }

        for (int v = 0; v < variants.Count; v++)
        {
            var variant = variants[v];
            ConsoleReporter.VariantHeader(log, variant.Label, warmup, iterations);

            for (int pass = 0; pass < warmup + iterations; pass++)
            {
                bool record = pass >= warmup;
                var ctx = new UseCaseContext { VariantLabel = variant.Label, Log = log, Ct = ct };
                seedContext?.Invoke(ctx);   // constant resources (provider, config)
                variant.Apply(ctx);         // variant-specific state (e.g. data generator)

                var sw = Stopwatch.StartNew();
                for (int s = 0; s < steps.Count; s++)
                {
                    ct.ThrowIfCancellationRequested();
                    var result = await steps[s].RunAsync(ctx);
                    if (record && result is not null)
                        samples[s][v].Add(result);
                }
                ConsoleReporter.IterationDone(log, pass, warmup, iterations, sw.Elapsed);
            }

            log.Line("");
        }

        var rows = new List<StepRow>(steps.Count);
        for (int s = 0; s < steps.Count; s++)
        {
            var cells = new StepStats?[variants.Count];
            for (int v = 0; v < variants.Count; v++)
                cells[v] = samples[s][v].Count > 0 ? StepStats.From(samples[s][v]) : null;
            rows.Add(new StepRow(steps[s].Name, cells));
        }

        var report = new UseCaseReport(
            meta, EnvironmentInfo.Capture(), variants.Select(v => v.Label).ToList(), rows, warmup, iterations);

        ConsoleReporter.Print(log, report);   // consolidated, side-by-side table
        return report;
    }
}

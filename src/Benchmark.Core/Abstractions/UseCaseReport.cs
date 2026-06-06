namespace Benchmark.Core;

/// <summary>
/// The complete, engine-agnostic result of running a use case: metadata, the environment it ran in,
/// the variant column labels (index 0 = baseline) and one <see cref="StepRow"/> per step. Each cell is
/// an aggregate (<see cref="StepStats"/>) over the measured iterations.
/// </summary>
public sealed record UseCaseReport(
    UseCaseMetadata        Metadata,
    EnvironmentInfo        Environment,
    IReadOnlyList<string>  Variants,
    IReadOnlyList<StepRow> Rows,
    int                    Warmup     = 0,
    int                    Iterations = 1);

/// <summary>One step's aggregated results across every variant. <see cref="Cells"/> is aligned with the
/// report's <see cref="UseCaseReport.Variants"/>; a cell may be <c>null</c> if the step was skipped.</summary>
public sealed record StepRow(string Step, IReadOnlyList<StepStats?> Cells);

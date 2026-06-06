using Benchmark.Core;

/// <summary>Verifies that regenerating a report overwrites the table but keeps hand-written notes.</summary>
public class MarkdownReporterTests
{
    private static UseCaseReport Report() => new(
        new UseCaseMetadata("md.test", "T", "Database", BenchmarkEngine.Stopwatch, "d"),
        EnvironmentInfo.Capture(),
        ["A"],
        [new StepRow("step", [StepStats.From([new StepResult("u", 1, "u")])])]);

    [Fact]
    public async Task WriteAsync_OnRegeneration_PreservesNotesBelowMarker()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bench-md-" + Guid.NewGuid().ToString("N"));
        try
        {
            // First write seeds the default notes block (with the marker).
            var path = await MarkdownReporter.WriteAsync(Report(), dir);
            Assert.Contains(MarkdownReporter.NotesMarker, await File.ReadAllTextAsync(path));

            // Author hand-written notes under the marker.
            await File.WriteAllTextAsync(path, await File.ReadAllTextAsync(path) + "\nMY HAND-WRITTEN NOTE\n");

            // Regenerating must keep them.
            await MarkdownReporter.WriteAsync(Report(), dir);
            var after = await File.ReadAllTextAsync(path);

            Assert.Contains(MarkdownReporter.NotesMarker, after);
            Assert.Contains("MY HAND-WRITTEN NOTE", after);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}

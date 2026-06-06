using System.Text.Json;
using System.Text.Json.Serialization;

namespace Benchmark.Core;

/// <summary>
/// Serialises a <see cref="UseCaseReport"/> to JSON (and back). Machine-readable output for history,
/// regression gating, dashboards or CI. Uses BCL <c>System.Text.Json</c>, so Core stays dependency-free.
/// </summary>
public static class JsonReporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters    = { new JsonStringEnumConverter() },
    };

    public static string Render(UseCaseReport report) => JsonSerializer.Serialize(report, Options);

    public static UseCaseReport Read(string json) =>
        JsonSerializer.Deserialize<UseCaseReport>(json, Options)
        ?? throw new InvalidOperationException("Could not deserialize the report JSON.");

    public static async Task<string> WriteAsync(UseCaseReport report, string resultsDir, CancellationToken ct = default)
    {
        Directory.CreateDirectory(resultsDir);
        var path = Path.Combine(resultsDir, report.Metadata.Id + ".json");
        await File.WriteAllTextAsync(path, Render(report), ct);
        return path;
    }

    public static async Task<UseCaseReport> ReadFileAsync(string path, CancellationToken ct = default) =>
        Read(await File.ReadAllTextAsync(path, ct));
}

/// <summary>
/// Locates the repo's <c>Results/</c> folder from the test bin directory, so integration runs write
/// their generated reports next to the committed ones (not into bin/). Walks up to the folder that
/// contains <c>Benchmark.sln</c>.
/// </summary>
internal static class TestPaths
{
    public static string ResultsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Benchmark.sln")))
            dir = dir.Parent;

        var root = dir?.FullName ?? Directory.GetCurrentDirectory();
        return Path.Combine(root, "Results");
    }
}

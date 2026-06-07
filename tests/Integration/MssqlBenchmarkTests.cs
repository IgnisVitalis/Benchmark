using Benchmark.Core;
using Benchmark.UseCases.Database;
using Benchmark.UseCases.Database.Mssql;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;
using Xunit.Abstractions;

public class MssqlBenchmarkTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .Build();

    private readonly DatabaseConfig _config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.MsSql.json")
        .Build()
        .Get<DatabaseConfig>() ?? new DatabaseConfig();

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync()    => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Run()
    {
        var adminConnStr = _container.GetConnectionString();
        var connStr = new SqlConnectionStringBuilder(adminConnStr) { InitialCatalog = "Benchmark" }.ConnectionString;

        var useCase = new MssqlUuidInsertUseCase(_config, connStr, adminConnStr);
        var report  = await useCase.RunAsync(new HostContext(
            new DelegateRunLog(output.WriteLine), iterations: TestRun.Iterations, warmup: TestRun.Warmup));

        Assert.Equal(2, report.Variants.Count);
        Assert.NotEmpty(report.Rows);
        Assert.All(report.Rows, r => Assert.Equal(TestRun.Iterations, r.Cells[0]!.Samples));   // measured iterations aggregated

        // Write the real report into the repo Results/ folder (Markdown + JSON, like the CLI).
        var path = await MarkdownReporter.WriteAsync(report, TestPaths.ResultsDir());
        await JsonReporter.WriteAsync(report, TestPaths.ResultsDir());
        await CompactReporter.WriteAsync(report, TestPaths.ResultsDir());
        output.WriteLine($"Report: {path}");
    }
}

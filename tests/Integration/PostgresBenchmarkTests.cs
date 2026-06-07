using Benchmark.Core;
using Benchmark.UseCases.Database;
using Benchmark.UseCases.Database.Postgres;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

public class PostgresBenchmarkTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    private readonly DatabaseConfig _config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.PostgreSQL.json")
        .Build()
        .Get<DatabaseConfig>() ?? new DatabaseConfig();

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync()    => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Run()
    {
        var adminConnStr = _container.GetConnectionString();
        var connStr = new NpgsqlConnectionStringBuilder(adminConnStr) { Database = "Benchmark" }.ConnectionString;

        var useCase = new PostgresUuidInsertUseCase(_config, connStr, adminConnStr);
        var report  = await useCase.RunAsync(new HostContext(
            new DelegateRunLog(output.WriteLine), iterations: TestRun.Iterations, warmup: TestRun.Warmup));

        Assert.Equal(2, report.Variants.Count);     // Guid.NewGuid() + Guid v7
        Assert.NotEmpty(report.Rows);
        Assert.All(report.Rows, r => Assert.Equal(TestRun.Iterations, r.Cells[0]!.Samples));   // measured iterations aggregated

        // Write the real report into the repo Results/ folder (Markdown + JSON, like the CLI).
        var path = await MarkdownReporter.WriteAsync(report, TestPaths.ResultsDir());
        await JsonReporter.WriteAsync(report, TestPaths.ResultsDir());
        output.WriteLine($"Report: {path}");
    }
}

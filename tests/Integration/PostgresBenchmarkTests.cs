using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

public class PostgresBenchmarkTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    private readonly BenchmarkConfig _config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.PostgreSQL.json")
        .Build()
        .Get<BenchmarkConfig>() ?? new BenchmarkConfig();

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync()    => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Run()
    {
        var adminConnStr = _container.GetConnectionString();
        var connStr = new NpgsqlConnectionStringBuilder(adminConnStr) { Database = "Benchmark" }.ConnectionString;

        await using var provider = new PostgresProvider(connStr, adminConnStr);
        await new BenchmarkRunner(_config, new XUnitLogger(output)).RunAsync(provider);
    }
}

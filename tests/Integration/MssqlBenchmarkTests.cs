using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;
using Xunit.Abstractions;

public class MssqlBenchmarkTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .Build();

    private readonly BenchmarkConfig _config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.MsSql.json")
        .Build()
        .Get<BenchmarkConfig>() ?? new BenchmarkConfig();

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync()    => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Run()
    {
        var adminConnStr = _container.GetConnectionString();
        var connStr = new SqlConnectionStringBuilder(adminConnStr) { InitialCatalog = "Benchmark" }.ConnectionString;

        await using var provider = new MssqlProvider(connStr, adminConnStr);
        await new BenchmarkRunner(_config, output.WriteLine).RunAsync(provider);
    }
}

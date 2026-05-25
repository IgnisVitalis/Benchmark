using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .Build()
    .Get<BenchmarkConfig>() ?? new BenchmarkConfig();

foreach (var pc in config.Providers.Where(p => p.Enabled))
{
    IDbProvider provider = pc.Type switch
    {
        "PostgreSQL" => new PostgresProvider(pc.ConnStr, pc.AdminConnStr),
        "MSSQL"      => new MssqlProvider(pc.ConnStr, pc.AdminConnStr),
        _            => throw new InvalidOperationException($"Unknown provider type: {pc.Type}")
    };

    await using (provider)
        await new BenchmarkRunner(config).RunAsync(provider);

    Console.WriteLine();
}

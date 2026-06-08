using Benchmark.Core;
using Benchmark.UseCases.Database.DeviceViews;
using Benchmark.UseCases.Database.DeviceViews.Mongo;
using Benchmark.UseCases.Database.DeviceViews.Postgres;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

public class DeviceViewsBenchmarkTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg    = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    private readonly MongoDbContainer    _mongo = new MongoDbBuilder().Build();

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        await _mongo.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _pg.DisposeAsync();
        await _mongo.DisposeAsync();
    }

    [Fact]
    public async Task Run()
    {
        var pg    = _pg.GetConnectionString();
        var mongo = _mongo.GetConnectionString();

        // Correctness first: prove each store actually returns the expected matches, so we are not
        // benchmarking a silently-broken query.
        await AssertStoresReturnExpectedMatches(pg, mongo);

        var cfg = new DeviceViewsConfig
        {
            DeviceCount     = 1_000_000,
            LookupCount     = 500,
            ScanLookupCount = 20,
            ConnStr         = pg,
            MongoConnStr    = mongo,
        };

        var useCase = new DeviceViewsUseCase(cfg,
        [
            new NamedStore("Relational", new PostgresRelationalDeviceStore(pg)),
            new NamedStore("JSONB",      new PostgresJsonbDeviceStore(pg)),
            new NamedStore("MongoDB",    new MongoDeviceStore(mongo)),
        ]);

        var report = await useCase.RunAsync(new HostContext(
            new DelegateRunLog(output.WriteLine), iterations: 1, warmup: 0));

        Assert.Equal(3, report.Variants.Count);            // Relational + JSONB + MongoDB
        Assert.Equal(7, report.Rows.Count);                // load + 5 query steps + size
        Assert.All(report.Rows, row =>                     // every cell present, with the full sample count
            Assert.All(row.Cells, cell =>
            {
                Assert.NotNull(cell);
                Assert.Equal(1, cell!.Samples);
            }));

        await MarkdownReporter.WriteAsync(report, TestPaths.ResultsDir());
        await JsonReporter.WriteAsync(report, TestPaths.ResultsDir());
        await CompactReporter.WriteAsync(report, TestPaths.ResultsDir());
    }

    // Loads a small known dataset into each store and checks every query returns the expected matches —
    // present uuid → 1, absent uuid → 0, serial → 1, model → ≥1, update-by-uuid → 1.
    private static async Task AssertStoresReturnExpectedMatches(string pg, string mongo)
    {
        const int count = 500;
        var known = DeviceGenerator.Generate(123);

        IDeviceStore[] stores =
        [
            new PostgresRelationalDeviceStore(pg),
            new PostgresJsonbDeviceStore(pg),
            new MongoDeviceStore(mongo),
        ];

        foreach (var store in stores)
        {
            await store.ResetAsync(default);
            await store.InsertManyAsync(DeviceGenerator.Stream(count), default);

            Assert.Equal(1, await store.FindByUuidAsync([known.Uuid], 1, default));
            Assert.Equal(0, await store.FindByUuidAsync([Guid.NewGuid()], 1, default));
            Assert.Equal(1, await store.FindBySerialAsync([known.SerialNumber], 1, default));
            Assert.True(await store.FindByModelAsync([known.Model], 1, count, default) >= 1);
            Assert.Equal(1, await store.UpdateLastSeenAsync([known.Uuid], 1, DateTimeOffset.UtcNow, default));

            await store.DisposeAsync();
        }
    }
}

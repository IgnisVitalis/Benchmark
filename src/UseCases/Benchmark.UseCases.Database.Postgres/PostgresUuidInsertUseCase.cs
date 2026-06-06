using Benchmark.Core;

namespace Benchmark.UseCases.Database.Postgres;

/// <summary>The UUID insert/read use case bound to PostgreSQL (COPY bulk path, Npgsql driver).</summary>
public sealed class PostgresUuidInsertUseCase(DatabaseConfig cfg, string connStr, string adminConnStr)
    : DbInsertUseCase(cfg)
{
    public override UseCaseMetadata Metadata { get; } = new(
        Id:          "database.postgres.uuid-insert",
        Title:       "PostgreSQL — UUID insert & read patterns",
        Category:    "Database",
        Engine:      BenchmarkEngine.Stopwatch,
        Description: "Random Guid.NewGuid() vs time-ordered Guid v7 across bulk, single, prepared, "
                   + "shared-transaction and batched inserts, plus index size and point lookups on a "
                   + "fresh table. Shows how key ordering affects B-tree page splits.");

    protected override IDbProvider CreateProvider() => new PostgresProvider(connStr, adminConnStr);
}

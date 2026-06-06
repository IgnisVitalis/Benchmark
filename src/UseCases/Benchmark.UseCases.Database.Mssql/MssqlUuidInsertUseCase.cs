using Benchmark.Core;

namespace Benchmark.UseCases.Database.Mssql;

/// <summary>The UUID insert/read use case bound to SQL Server (SqlBulkCopy, Microsoft.Data.SqlClient).</summary>
public sealed class MssqlUuidInsertUseCase(DatabaseConfig cfg, string connStr, string adminConnStr)
    : DbInsertUseCase(cfg)
{
    public override UseCaseMetadata Metadata { get; } = new(
        Id:          "database.mssql.uuid-insert",
        Title:       "SQL Server — UUID insert & read patterns",
        Category:    "Database",
        Engine:      BenchmarkEngine.Stopwatch,
        Description: "Random Guid.NewGuid() vs time-ordered Guid v7 across bulk, single, prepared, "
                   + "shared-transaction and batched inserts, plus index size and point lookups on a "
                   + "fresh table. Shows how key ordering affects clustered-index page splits.");

    protected override IDbProvider CreateProvider() => new MssqlProvider(connStr, adminConnStr);
}

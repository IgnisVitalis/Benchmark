using System.Data.Common;

public interface IDbProvider : IAsyncDisposable
{
    string Name { get; }

    Task CreateDatabaseAsync();
    Task DropDatabaseAsync();
    Task SetupTableAsync();
    Task TruncateAsync();

    // Provider-specific: COPY for Postgres, SqlBulkCopy for MSSQL, etc.
    Task BulkInsertAsync(BenchRow[] rows);

    // Generic ADO.NET connection for portable scenarios (single, prepared, shared, batched)
    Task<DbConnection> OpenConnectionAsync();

    // Builds and prepares an INSERT command; each provider uses its own native parameter types
    Task<DbCommand> BuildPreparedInsertCommandAsync(DbConnection conn, BenchRow row);

    Task<string> GetIndexSizeAsync();
    Task<Guid[]> SampleIdsAsync(int count);
}

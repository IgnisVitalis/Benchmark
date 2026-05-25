using System.Data.Common;

interface IDbProvider : IAsyncDisposable
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

    Task<string> GetIndexSizeAsync();
    Task<Guid[]> SampleIdsAsync(int count);
}

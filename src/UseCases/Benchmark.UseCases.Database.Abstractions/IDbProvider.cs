using System.Data.Common;

namespace Benchmark.UseCases.Database;

/// <summary>
/// One database engine. Provider-specific concerns (DDL dialect, bulk protocol, native parameter
/// types) live here; the steps stay portable by talking only to this interface plus generic ADO.NET.
/// Implementations live in their own projects and pull in their own driver dependency.
/// </summary>
public interface IDbProvider : IAsyncDisposable
{
    string Name { get; }

    Task CreateDatabaseAsync();
    Task DropDatabaseAsync();
    Task SetupTableAsync();
    Task TruncateAsync();

    /// <summary>Provider-native bulk load: COPY for PostgreSQL, SqlBulkCopy for MSSQL, etc.</summary>
    Task BulkInsertAsync(BenchRow[] rows);

    /// <summary>Generic open ADO.NET connection for the portable insert/lookup steps.</summary>
    Task<DbConnection> OpenConnectionAsync();

    /// <summary>Builds and prepares an INSERT command using the provider's native parameter types.</summary>
    Task<DbCommand> BuildPreparedInsertCommandAsync(DbConnection conn, BenchRow row);

    /// <summary>Primary-key index size, in megabytes.</summary>
    Task<double> GetIndexSizeAsync();

    Task<Guid[]> SampleIdsAsync(int count);
}

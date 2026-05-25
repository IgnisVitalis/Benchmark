using System.Data.Common;
using Npgsql;
using NpgsqlTypes;

class PostgresProvider(string connStr, string adminConnStr) : IDbProvider
{
    public string Name => "PostgreSQL";

    public async Task CreateDatabaseAsync()
    {
        await using var conn = new NpgsqlConnection(adminConnStr);
        await conn.OpenAsync();

        await Exec(conn, $"""
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = 'Benchmark' AND pid <> pg_backend_pid()
            """);
        await Exec(conn, "DROP DATABASE IF EXISTS \"Benchmark\"");
        await Exec(conn, "CREATE DATABASE \"Benchmark\"");

        Console.WriteLine("Database 'Benchmark' created.\n");
    }

    public async Task DropDatabaseAsync()
    {
        await using var conn = new NpgsqlConnection(adminConnStr);
        await conn.OpenAsync();

        await Exec(conn, $"""
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = 'Benchmark' AND pid <> pg_backend_pid()
            """);
        await Exec(conn, "DROP DATABASE \"Benchmark\"");

        Console.WriteLine("\nDatabase 'Benchmark' dropped.");
    }

    public async Task SetupTableAsync()
    {
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();
        await Exec(conn, """
            DROP TABLE IF EXISTS benchmark_rows;
            CREATE TABLE benchmark_rows (
                id          uuid             PRIMARY KEY,
                col_text    text             NOT NULL,
                col_varchar varchar(100)     NOT NULL,
                col_decimal numeric(18,4)    NOT NULL,
                col_int     integer          NOT NULL,
                col_long    bigint           NOT NULL,
                col_bool    boolean          NOT NULL,
                col_ts      timestamptz      NOT NULL,
                col_double  double precision NOT NULL,
                col_short   smallint         NOT NULL
            );
            """);
        Console.WriteLine("Table ready.\n");
    }

    public async Task TruncateAsync()
    {
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();
        await Exec(conn, "TRUNCATE benchmark_rows");
    }

    public async Task BulkInsertAsync(BenchRow[] rows)
    {
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        await using var writer = await conn.BeginBinaryImportAsync(
            "COPY benchmark_rows (id,col_text,col_varchar,col_decimal,col_int,col_long,col_bool,col_ts,col_double,col_short) FROM STDIN (FORMAT BINARY)");

        foreach (var row in rows)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(row.Id,         NpgsqlDbType.Uuid);
            await writer.WriteAsync(row.ColText,    NpgsqlDbType.Text);
            await writer.WriteAsync(row.ColVarchar, NpgsqlDbType.Varchar);
            await writer.WriteAsync(row.ColDecimal, NpgsqlDbType.Numeric);
            await writer.WriteAsync(row.ColInt,     NpgsqlDbType.Integer);
            await writer.WriteAsync(row.ColLong,    NpgsqlDbType.Bigint);
            await writer.WriteAsync(row.ColBool,    NpgsqlDbType.Boolean);
            await writer.WriteAsync(row.ColTs,      NpgsqlDbType.TimestampTz);
            await writer.WriteAsync(row.ColDouble,  NpgsqlDbType.Double);
            await writer.WriteAsync(row.ColShort,   NpgsqlDbType.Smallint);
        }

        await writer.CompleteAsync();
    }

    public async Task<DbConnection> OpenConnectionAsync()
    {
        var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();
        return conn;
    }

    public async Task<string> GetIndexSizeAsync()
    {
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT pg_size_pretty(pg_indexes_size('benchmark_rows'))";
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task<Guid[]> SampleIdsAsync(int count)
    {
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT id FROM benchmark_rows TABLESAMPLE SYSTEM(1) LIMIT {count}";
        var ids = new List<Guid>(count);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            ids.Add(r.GetGuid(0));
        return ids.ToArray();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task Exec(NpgsqlConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}

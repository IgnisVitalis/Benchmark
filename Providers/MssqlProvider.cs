using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;

class MssqlProvider(string connStr, string adminConnStr) : IDbProvider
{
    public string Name => "MSSQL";

    public async Task CreateDatabaseAsync()
    {
        await using var conn = new SqlConnection(adminConnStr);
        await conn.OpenAsync();

        await Exec(conn, """
            IF EXISTS (SELECT 1 FROM sys.databases WHERE name = 'Benchmark')
            BEGIN
                ALTER DATABASE [Benchmark] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [Benchmark];
            END
            """);
        await Exec(conn, "CREATE DATABASE [Benchmark]");

        Console.WriteLine("Database 'Benchmark' created.\n");
    }

    public async Task DropDatabaseAsync()
    {
        await using var conn = new SqlConnection(adminConnStr);
        await conn.OpenAsync();

        await Exec(conn, """
            ALTER DATABASE [Benchmark] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            DROP DATABASE [Benchmark];
            """);

        Console.WriteLine("\nDatabase 'Benchmark' dropped.");
    }

    public async Task SetupTableAsync()
    {
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();
        await Exec(conn, """
            IF OBJECT_ID('benchmark_rows', 'U') IS NOT NULL
                DROP TABLE benchmark_rows;
            CREATE TABLE benchmark_rows (
                id          uniqueidentifier NOT NULL PRIMARY KEY,
                col_text    nvarchar(MAX)    NOT NULL,
                col_varchar nvarchar(100)    NOT NULL,
                col_decimal decimal(18,4)    NOT NULL,
                col_int     int              NOT NULL,
                col_long    bigint           NOT NULL,
                col_bool    bit              NOT NULL,
                col_ts      datetimeoffset   NOT NULL,
                col_double  float            NOT NULL,
                col_short   smallint         NOT NULL
            );
            """);
        Console.WriteLine("Table ready.\n");
    }

    public async Task TruncateAsync()
    {
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();
        await Exec(conn, "TRUNCATE TABLE benchmark_rows");
    }

    public async Task BulkInsertAsync(BenchRow[] rows)
    {
        var table = BuildDataTable(rows);

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        using var bulk = new SqlBulkCopy(conn)
        {
            DestinationTableName = "benchmark_rows",
            BatchSize            = 10_000,
        };
        MapColumns(bulk);
        await bulk.WriteToServerAsync(table);
    }

    public async Task<DbConnection> OpenConnectionAsync()
    {
        var conn = new SqlConnection(connStr);
        await conn.OpenAsync();
        return conn;
    }

    public async Task<string> GetIndexSizeAsync()
    {
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT CAST(ROUND(SUM(a.used_pages) * 8.0 / 1024, 2) AS decimal(10,2))
            FROM sys.tables t
            JOIN sys.indexes i ON t.object_id = i.object_id
            JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id
            JOIN sys.allocation_units a ON p.partition_id = a.container_id
            WHERE t.name = 'benchmark_rows' AND i.type > 0
            """;
        var mb = (decimal)(await cmd.ExecuteScalarAsync())!;
        return $"{mb} MB";
    }

    public async Task<Guid[]> SampleIdsAsync(int count)
    {
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT TOP {count} id FROM benchmark_rows TABLESAMPLE (1 PERCENT)";
        var ids = new List<Guid>(count);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            ids.Add(r.GetGuid(0));
        return ids.ToArray();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ─── helpers ──────────────────────────────────────────────────────────────

    private static DataTable BuildDataTable(BenchRow[] rows)
    {
        var dt = new DataTable();
        dt.Columns.Add("id",          typeof(Guid));
        dt.Columns.Add("col_text",    typeof(string));
        dt.Columns.Add("col_varchar", typeof(string));
        dt.Columns.Add("col_decimal", typeof(decimal));
        dt.Columns.Add("col_int",     typeof(int));
        dt.Columns.Add("col_long",    typeof(long));
        dt.Columns.Add("col_bool",    typeof(bool));
        dt.Columns.Add("col_ts",      typeof(DateTimeOffset));
        dt.Columns.Add("col_double",  typeof(double));
        dt.Columns.Add("col_short",   typeof(short));

        foreach (var row in rows)
            dt.Rows.Add(row.Id, row.ColText, row.ColVarchar, row.ColDecimal,
                        row.ColInt, row.ColLong, row.ColBool, row.ColTs,
                        row.ColDouble, row.ColShort);
        return dt;
    }

    private static void MapColumns(SqlBulkCopy bulk)
    {
        foreach (string col in new[]
            { "id", "col_text", "col_varchar", "col_decimal",
              "col_int", "col_long", "col_bool", "col_ts", "col_double", "col_short" })
            bulk.ColumnMappings.Add(col, col);
    }

    private static async Task Exec(SqlConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}

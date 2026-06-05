using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;

public class MssqlProvider(string connStr, string adminConnStr) : IDbProvider
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
    }

    public async Task DropDatabaseAsync()
    {
        await using var conn = new SqlConnection(adminConnStr);
        await conn.OpenAsync();

        await Exec(conn, """
            ALTER DATABASE [Benchmark] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            DROP DATABASE [Benchmark];
            """);
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
    }

    public async Task TruncateAsync()
    {
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();
        await Exec(conn, "TRUNCATE TABLE benchmark_rows");
    }

    public async Task BulkInsertAsync(BenchRow[] rows)
    {
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        using var bulk = new SqlBulkCopy(conn)
        {
            DestinationTableName = "benchmark_rows",
            BatchSize            = 10_000,
        };
        MapColumns(bulk);
        await bulk.WriteToServerAsync(new BenchRowReader(rows));
    }

    public async Task<DbConnection> OpenConnectionAsync()
    {
        var conn = new SqlConnection(connStr);
        await conn.OpenAsync();
        return conn;
    }

    public async Task<DbCommand> BuildPreparedInsertCommandAsync(DbConnection conn, BenchRow row)
    {
        var cmd = (SqlCommand)conn.CreateCommand();
        cmd.CommandText = InsertHelpers.InsertSql;
        Sp(cmd, "@id",      row.Id,          SqlDbType.UniqueIdentifier);
        Sp(cmd, "@text",    row.ColText,      SqlDbType.NVarChar,       size: 4000);
        Sp(cmd, "@varchar", row.ColVarchar,   SqlDbType.NVarChar,       size: 100);
        Sp(cmd, "@dec",     row.ColDecimal,   SqlDbType.Decimal,        precision: 18, scale: 4);
        Sp(cmd, "@int",     row.ColInt,       SqlDbType.Int);
        Sp(cmd, "@lng",     row.ColLong,      SqlDbType.BigInt);
        Sp(cmd, "@bool",    row.ColBool,      SqlDbType.Bit);
        Sp(cmd, "@ts",      row.ColTs,        SqlDbType.DateTimeOffset, size: 7, scale: 7);
        Sp(cmd, "@dbl",     row.ColDouble,    SqlDbType.Float);
        Sp(cmd, "@short",   row.ColShort,     SqlDbType.SmallInt);
        await cmd.PrepareAsync();
        return cmd;
    }

    private static void Sp(SqlCommand cmd, string name, object value, SqlDbType type,
                           int size = 0, byte precision = 0, byte scale = 0)
    {
        var p = new SqlParameter(name, type);
        p.Value = value;                            // Value first — must come before Size/Scale
        if (size      > 0) p.Size      = size;      // then override; Value setter resets these flags
        if (precision > 0) p.Precision = precision;
        if (scale     > 0) p.Scale     = scale;
        cmd.Parameters.Add(p);
    }

    public async Task<string> GetIndexSizeAsync()
    {
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        // IN_ROW_DATA and ROW_OVERFLOW_DATA use hobt_id; LOB_DATA uses partition_id
        cmd.CommandText = """
            SELECT CAST(ROUND(SUM(a.used_pages) * 8.0 / 1024, 2) AS decimal(10,2))
            FROM sys.tables t
            JOIN sys.indexes i ON t.object_id = i.object_id
            JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id
            JOIN sys.allocation_units a ON
                (a.type IN (1, 3) AND a.container_id = p.hobt_id) OR
                (a.type = 2     AND a.container_id = p.partition_id)
            WHERE t.name = 'benchmark_rows' AND i.type > 0
            """;
        var result = await cmd.ExecuteScalarAsync();
        var mb = result is DBNull or null ? 0m : (decimal)result;
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

    // Streams BenchRow[] to SqlBulkCopy without materialising a DataTable copy
    private sealed class BenchRowReader(BenchRow[] rows) : IDataReader
    {
        private int _pos = -1;
        private BenchRow Row => rows[_pos];

        public bool Read()          => ++_pos < rows.Length;
        public bool NextResult()    => false;
        public void Close()         => _pos = rows.Length;
        public void Dispose()       { }
        public bool IsClosed        => _pos >= rows.Length;
        public int  RecordsAffected => -1;
        public int  Depth           => 0;
        public DataTable? GetSchemaTable() => null;

        public int    FieldCount            => 10;
        public object this[int i]           => GetValue(i);
        public object this[string name]     => GetValue(GetOrdinal(name));

        public string GetName(int i) => i switch
        {
            0 => "id",          1 => "col_text",    2 => "col_varchar",
            3 => "col_decimal", 4 => "col_int",     5 => "col_long",
            6 => "col_bool",    7 => "col_ts",      8 => "col_double",
            9 => "col_short",   _ => throw new IndexOutOfRangeException()
        };

        public int GetOrdinal(string name) => name switch
        {
            "id" => 0,          "col_text" => 1,    "col_varchar" => 2,
            "col_decimal" => 3, "col_int" => 4,     "col_long" => 5,
            "col_bool" => 6,    "col_ts" => 7,      "col_double" => 8,
            "col_short" => 9,   _ => throw new IndexOutOfRangeException()
        };

        public object GetValue(int i) => i switch
        {
            0 => Row.Id,
            1 => Row.ColText,
            2 => Row.ColVarchar,
            3 => Row.ColDecimal,
            4 => Row.ColInt,
            5 => Row.ColLong,
            6 => Row.ColBool,
            7 => Row.ColTs,
            8 => Row.ColDouble,
            9 => Row.ColShort,
            _ => throw new IndexOutOfRangeException()
        };

        public int GetValues(object[] values)
        {
            var n = Math.Min(values.Length, FieldCount);
            for (int i = 0; i < n; i++) values[i] = GetValue(i);
            return n;
        }

        public bool    IsDBNull(int i)   => false;
        public bool    GetBoolean(int i) => (bool)GetValue(i);
        public byte    GetByte(int i)    => (byte)GetValue(i);
        public char    GetChar(int i)    => (char)GetValue(i);
        public Guid    GetGuid(int i)    => (Guid)GetValue(i);
        public short   GetInt16(int i)   => (short)GetValue(i);
        public int     GetInt32(int i)   => (int)GetValue(i);
        public long    GetInt64(int i)   => (long)GetValue(i);
        public float   GetFloat(int i)   => (float)GetValue(i);
        public double  GetDouble(int i)  => (double)GetValue(i);
        public decimal GetDecimal(int i) => (decimal)GetValue(i);
        public string  GetString(int i)  => (string)GetValue(i);
        public DateTime GetDateTime(int i) => ((DateTimeOffset)GetValue(i)).UtcDateTime;
        public string  GetDataTypeName(int i) => GetFieldType(i).Name;
        public Type    GetFieldType(int i)    => GetValue(i).GetType();
        public IDataReader GetData(int i)     => throw new NotSupportedException();
        public long GetBytes(int i, long fo, byte[]? b, int bo, int l) => 0;
        public long GetChars(int i, long fo, char[]? b, int bo, int l) => 0;
    }
}

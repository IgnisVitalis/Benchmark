using System.Data;
using System.Data.Common;
using System.Text;

namespace Benchmark.UseCases.Database;

/// <summary>Shared SQL text, row generation and ADO.NET parameter binding used by the insert steps.</summary>
public static class InsertHelpers
{
    public const string InsertSql = """
        INSERT INTO benchmark_rows
            (id, col_text, col_varchar, col_decimal, col_int, col_long, col_bool, col_ts, col_double, col_short)
        VALUES
            (@id, @text, @varchar, @dec, @int, @lng, @bool, @ts, @dbl, @short)
        """;

    private static readonly Random Rng = new(42);

    public static BenchRow[] PrepareRows(int count, Func<Guid> newId)
    {
        var rows = new BenchRow[count];
        for (int i = 0; i < count; i++)
            rows[i] = MakeRow(newId());
        return rows;
    }

    public static BenchRow MakeRow(Guid id) => new(
        id,
        $"text-{id:N}",
        $"vc-{Rng.Next(99_999):D5}",
        Math.Round((decimal)(Rng.NextDouble() * 100_000), 4),
        Rng.Next(),
        ((long)Rng.Next() << 32) | (uint)Rng.Next(),
        Rng.Next(2) == 1,
        DateTimeOffset.UtcNow.AddSeconds(-Rng.Next(86_400 * 365)),
        Rng.NextDouble() * 1_000,
        (short)Rng.Next(short.MaxValue));

    public static DbCommand BuildInsertCommand(DbConnection conn, BenchRow row)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = InsertSql;
        AddInsertParams(cmd, row);
        return cmd;
    }

    public static void AddInsertParams(DbCommand cmd, BenchRow row, string suffix = "")
    {
        P(cmd, "id"      + suffix, row.Id,         DbType.Guid);
        P(cmd, "text"    + suffix, row.ColText,     DbType.String,  4000);
        P(cmd, "varchar" + suffix, row.ColVarchar,  DbType.String,  100);
        P(cmd, "dec"     + suffix, row.ColDecimal,  DbType.Decimal, precision: 18, scale: 4);
        P(cmd, "int"     + suffix, row.ColInt,      DbType.Int32);
        P(cmd, "lng"     + suffix, row.ColLong,     DbType.Int64);
        P(cmd, "bool"    + suffix, row.ColBool,     DbType.Boolean);
        P(cmd, "ts"      + suffix, row.ColTs,       DbType.DateTimeOffset, scale: 7);
        P(cmd, "dbl"     + suffix, row.ColDouble,   DbType.Double);
        P(cmd, "short"   + suffix, row.ColShort,    DbType.Int16);
    }

    public static void SetInsertValues(DbCommand cmd, BenchRow row)
    {
        cmd.Parameters[0].Value = row.Id;
        cmd.Parameters[1].Value = row.ColText;
        cmd.Parameters[2].Value = row.ColVarchar;
        cmd.Parameters[3].Value = row.ColDecimal;
        cmd.Parameters[4].Value = row.ColInt;
        cmd.Parameters[5].Value = row.ColLong;
        cmd.Parameters[6].Value = row.ColBool;
        cmd.Parameters[7].Value = row.ColTs;
        cmd.Parameters[8].Value = row.ColDouble;
        cmd.Parameters[9].Value = row.ColShort;
    }

    public static string BuildBatchedSql(int rowCount)
    {
        var sb = new StringBuilder(
            "INSERT INTO benchmark_rows (id,col_text,col_varchar,col_decimal,col_int,col_long,col_bool,col_ts,col_double,col_short) VALUES ");
        for (int i = 0; i < rowCount; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"(@id{i},@text{i},@varchar{i},@dec{i},@int{i},@lng{i},@bool{i},@ts{i},@dbl{i},@short{i})");
        }
        return sb.ToString();
    }

    private static void P(DbCommand cmd, string name, object value, DbType dbType,
                          int size = 0, byte precision = 0, byte scale = 0)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = "@" + name;
        p.DbType = dbType;
        if (size      != 0) p.Size      = size;
        if (precision != 0) p.Precision = precision;
        if (scale     != 0) p.Scale     = scale;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}

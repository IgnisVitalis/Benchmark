namespace Benchmark.UseCases.Database;

/// <summary>A single wide row inserted by the DB benchmark steps (mixed column types).</summary>
public record BenchRow(
    Guid           Id,
    string         ColText,
    string         ColVarchar,
    decimal        ColDecimal,
    int            ColInt,
    long           ColLong,
    bool           ColBool,
    DateTimeOffset ColTs,
    double         ColDouble,
    short          ColShort);

# SQLBenchmark

A .NET 9 console tool for benchmarking SQL databases across common insert and read patterns. Primary goals:

- **Learning** — understand how database internals (B-tree structure, transaction overhead, bulk protocols) affect real throughput numbers
- **Pre-production validation** — run the suite against a target database before going live to catch performance regressions or misconfiguration early

---

## What it tests

Each run executes two rounds against a fresh 10 million-row table — one with `Guid.NewGuid()` (random UUIDs) and one with Guid v7 (time-ordered UUIDs). The v7 round shows the gain percentage relative to the baseline.

| # | Scenario | Description |
|---|----------|-------------|
| 1 | Bulk insert N rows | Provider-native bulk protocol (COPY for PostgreSQL, SqlBulkCopy for MSSQL) |
| 2 | Index size | B-tree size after bulk insert — shows page fragmentation caused by random vs ordered keys |
| 3 | Point lookups | 1 000 prepared SELECT by primary key on a populated table |
| 4 | +100 rows, 1 tx/row | One transaction and one round-trip per row — baseline for single inserts |
| 5 | +100 rows, prepared, 1 tx/row | Reuses a prepared statement; eliminates parse/plan overhead per row |
| 6 | +100 rows, 1 tx total | All rows in a single transaction — removes per-row fsync |
| 7 | +100 rows, batched VALUES | Single INSERT with all rows in one VALUES clause and one round-trip |

---

## Architecture

```
BenchmarkRunner
│  owns List<IBenchmarkCase>  (BuildCases)
│  orchestrates two rounds (NewGuid → Guid v7)
│
├── IBenchmarkCase          one class per test scenario
│     RunAsync(BenchmarkContext) → CaseResult?
│     null return = info-only (case prints its own line)
│
├── BenchmarkContext        shared state threaded through all cases
│     Provider, Config, NewId, SampledIds
│
└── IDbProvider             one class per database engine
      BulkInsertAsync       provider-specific bulk path
      OpenConnectionAsync   generic ADO.NET connection for portable cases
      GetIndexSizeAsync / SampleIdsAsync / DDL helpers
```

**Adding a new test case** — create a class in `Cases/`, implement `IBenchmarkCase`, register it in `BenchmarkRunner.BuildCases()`.

**Adding a new database** — implement `IDbProvider` in `Providers/`, enable it in `appsettings.json`.

---

## Configuration

`appsettings.json` controls row counts and which providers are active:

```json
{
  "BulkCount":   10000000,
  "SingleCount": 100,
  "LookupCount": 1000,
  "Providers": [
    { "Type": "PostgreSQL", "Enabled": true,  "ConnStr": "...", "AdminConnStr": "..." },
    { "Type": "MSSQL",      "Enabled": false, "ConnStr": "...", "AdminConnStr": "..." }
  ]
}
```

---

## Results

### PostgreSQL 17 — localhost — 2025-05-26

```
  #  Scenario                                            Ms     Rows/sec
----------------------------------------------------------------------
--- PostgreSQL / Guid.NewGuid() ---
  1  Bulk insert 10,000,000 rows                     140151       71,351
  2  Index size after bulk insert                               382 MB
  3  1,000 point lookups                                177        5,658
  4  +100 rows, 1 tx/row                                 57        1,752
  5  +100 rows, prepared, 1 tx/row                       51        1,968
  6  +100 rows, 1 tx total                               26        3,859
  7  +100 rows, batched VALUES                           11        8,979

--- PostgreSQL / Guid v7 ---
  1  Bulk insert 10,000,000 rows                      33430      299,133   +319.2%
  2  Index size after bulk insert                               394 MB
  3  1,000 point lookups                                119        8,394    +48.4%
  4  +100 rows, 1 tx/row                                 45        2,213    +26.3%
  5  +100 rows, prepared, 1 tx/row                       36        2,774    +41.0%
  6  +100 rows, 1 tx total                               23        4,297    +11.4%
  7  +100 rows, batched VALUES                            5       19,456   +116.7%
```

#### Key observations

- **Bulk insert (+319%)** — the largest win. Random UUIDs cause constant B-tree page splits as the index must reorder every incoming key. Time-ordered keys always append to the rightmost leaf, making splits rare.
- **Index size** — Guid v7 produces a *larger* index (394 MB vs 382 MB) despite being faster. Random inserts leave partially-filled pages after splits; ordered inserts pack pages more densely but the table itself is larger because fewer pages are reused.
- **Batched VALUES (+117%)** — the second-largest gain. With random UUIDs the single-statement insert still triggers many index rebalances; with v7 the tree barely changes shape.
- **Point lookups (+48%)** — v7 keys cluster related rows on the same index pages, improving cache hit rate even for random-access patterns after a warm-up period.
- **Shared tx vs 1 tx/row (+11%)** — the smallest relative gain because the dominant cost here is fsync per transaction, which is the same regardless of key order.

# Benchmark — a tech-agnostic performance-estimation framework

Define a **use case** — a database pattern, or anything else measurable — in its own project that pulls
in **only** the dependencies that use case needs. The framework runs it (optionally comparing variants),
prints a live table, and emits a Markdown report into [`Results/`](Results). The core carries **zero
third-party dependencies**.

- **Learning** — see how internals (B-tree page splits, transaction overhead, cache) move real numbers.
- **Pre-production validation** — run a suite against a target before go-live to catch regressions early.

> New here? Read **[AGENT.md](AGENT.md)** for the architecture, vocabulary, and step-by-step recipes.

---

## Quickstart

```bash
dotnet build Benchmark.sln -c Release

# from the repo root (so ./appsettings.json and ./Results resolve):
dotnet run --project src/Benchmark.Cli -c Release -- list
dotnet run --project src/Benchmark.Cli -c Release -- run database.postgres.uuid-insert
```

Each `run` writes `Results/<use-case-id>.md`. Configuration (row counts, DB connection strings, which
providers are enabled) lives in [`appsettings.json`](appsettings.json) under a per-domain section.

---

## Use-case catalog

Each use case maps 1:1 to one generated report. Add a row here when you add a use case.

| Use case (`id`) | Category | Engine | What it measures | Result |
|---|---|---|---|---|
| `database.postgres.uuid-insert` | Database | Stopwatch | Random `Guid.NewGuid()` vs time-ordered `Guid v7` across bulk / single / prepared / shared-tx / batched inserts, index size and point lookups on a fresh table. | [report](Results/database.postgres.uuid-insert.md) |
| `database.mssql.uuid-insert` | Database | Stopwatch | The same suite against SQL Server (SqlBulkCopy, clustered-index splits). | [report](Results/database.mssql.uuid-insert.md) |
| `database.device-views` | Database | Stopwatch | The same 20-field Device stored as relational columns, a single JSONB column, or MongoDB documents — load, lookups (indexed-unique / indexed / non-indexed), update, range query, storage size. | [report](Results/database.device-views.md) |

---

## Architecture (in one breath)

A **use case** is an ordered **chain of steps** that share a `UseCaseContext`; an earlier step can hand
state to a later one (bulk-insert populates the table, point-lookups reads it). The chain is re-run once
per **variant** (e.g. two UUID schemes), and every step reports its gain versus the baseline variant.
Steps are timed by an **engine** — currently the BCL `Stopwatch` harness. The model is engine-agnostic:
the engine's output normalises into one `UseCaseReport`, so additional engines can be added later without
touching the reporter.

```
Benchmark.Cli ──> UseCaseRegistry ──> IUseCase.RunAsync(HostContext)
                                          │
                ChainedUseCase ──> ChainRunner (steps × variants, live console)
                                          │
                                   UseCaseReport ──> MarkdownReporter ──> Results/<id>.md
```

The keystone: **`Benchmark.Core` depends only on the BCL.** Drivers (`Npgsql`, `Microsoft.Data.SqlClient`)
live only in the leaf use-case projects. A guard test enforces it.

---

## Project layout

```
src/
  Benchmark.Core/                                # dependency-free core (abstractions, runner, reporters)
  Benchmark.Cli/                                 # console host  (list | run …)
  UseCases/
    Benchmark.UseCases.Database.Abstractions/    # IDbProvider, the 7 steps, DbInsertUseCase  (dep-free)
    Benchmark.UseCases.Database.Postgres/        # Npgsql
    Benchmark.UseCases.Database.Mssql/           # Microsoft.Data.SqlClient
tests/Integration/                               # Testcontainers DB runs + architecture guards
Results/                                         # generated <id>.md  (+ _TEMPLATE.md)
appsettings.json   AGENT.md   CLAUDE.md
```

---

## Adding a use case

Full recipes (add a step, a DB engine, or a brand-new domain) are in **[AGENT.md §6](AGENT.md)**. In short:

1. Create a project that references `Benchmark.Core` (and only the deps your use case truly needs).
2. Implement steps (`IBenchmarkStep`) and a use case (`ChainedUseCase`).
3. Reference it from `Benchmark.Cli.csproj`, register it in `Program.cs`, add it to the solution and the
   catalog table above.

---

## Results convention

- One use case ⇒ one `Results/<id>.md` **and** `Results/<id>.json`, regenerated on each run (see [`Results/_TEMPLATE.md`](Results/_TEMPLATE.md)).
- Values are the **median ± CV%** over N measured iterations (after warm-up); the report embeds the environment for reproducibility.
- **Regression gate:** `baseline <id>` snapshots a reference, then `run <id> --gate` (or `compare <id>`) exits non-zero if a metric regresses beyond a threshold — see [AGENT.md](AGENT.md).
- Link every result from the catalog table. Don't hand-edit generated numbers — re-run instead.

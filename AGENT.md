# AGENT.md — contributor & agent guide

> Canonical guide for anyone (human or AI agent) working in this repository.
> `CLAUDE.md` defers to this file for substance and only adds Claude-Code-specific operational notes.
> (`AGENTS.md` is the common cross-tool alias for this file; we use the singular `AGENT.md`.)

---

## 1. What this project is

A **tech-agnostic framework for estimating the performance of "use cases"** — database patterns,
algorithms, or anything else you can measure. You define a use case once, in its own project, pulling
in only the dependencies that use case needs. The framework runs it, optionally comparing variants,
and emits a Markdown report into `Results/`.

Two goals, inherited from the original SQL benchmark:

- **Learning** — see how internals (B-tree splits, transaction overhead, JIT, cache) move real numbers.
- **Pre-production validation** — run a suite against a target before go-live to catch regressions.

---

## 2. Vocabulary (used in code and docs)

| Term | Type | Meaning |
|---|---|---|
| **Step** | `IBenchmarkStep` | The smallest measured unit (e.g. "bulk insert", "point lookups"). |
| **Use case** | `IUseCase` | A named, runnable bundle = an ordered **chain of steps** + variants + setup/teardown + metadata. Maps **1:1 to one `Results/<id>.md`**. |
| **Context** | `UseCaseContext` | A typed state bag threaded through the steps of one variant run. |
| **Variant** | `Variant` | One labelled run of the whole chain over different data/config. The first variant is the baseline; gain is reported relative to it. |
| **Provider / fixture** | e.g. `IDbProvider` | A domain resource a use case needs. Lives in the use case's own project with its own driver dependency. |
| **Engine** | `BenchmarkEngine` | How a step is timed. `Stopwatch` (BCL) drives the current DB/integration use cases. The model is engine-agnostic, so another engine (e.g. a BenchmarkDotNet adapter for micro-benchmarks) can be added later — see §6.3. |

### Chaining has three axes

1. **Intra-use-case** — steps run in order, sharing one `UseCaseContext`. An earlier step publishes
   state (a populated table, sampled ids) that a later step consumes. *That shared context is the chain.*
2. **Variants** — the same chain is re-run per variant (e.g. `Guid.NewGuid()` vs `Guid v7`), and every
   step reports its gain vs the baseline variant.
3. **Inter-use-case** — the host (`run --category` / `run --all`) runs several use cases in sequence.

---

## 3. The one hard rule: a dependency-free Core

**`Benchmark.Core` and `Benchmark.UseCases.Database.Abstractions` must reference ONLY `System.*` (the BCL).
Never add a NuGet `PackageReference` to them.**

Domain dependencies — `Npgsql`, `Microsoft.Data.SqlClient`, `BenchmarkDotNet`, an HTTP client, etc. —
belong **only** in the leaf use-case / engine projects that need them. This is what makes the framework
tech-agnostic: a consumer of the Postgres use case never drags in the SQL Server driver, and the Core
never drags in anything.

This invariant is enforced by `tests/Benchmark.UnitTests/ArchitectureTests.cs` (runs without Docker). If
you break it, that test fails.

---

## 4. Architecture & project layout

```
Benchmark.sln
├── src/
│   ├── Benchmark.Core/                                # dependency-free core (BCL only)
│   │   Abstractions/  IBenchmarkStep, IUseCase, UseCaseContext, StepResult,
│   │                  UseCaseMetadata, Variant, UseCaseReport, IRunLog, EnvironmentInfo
│   │   Chaining/      ChainRunner, ChainedUseCase
│   │   Engines/       StopwatchEngine
│   │   Reporting/     ConsoleReporter, MarkdownReporter
│   │   Hosting/       HostContext, UseCaseRegistry
│   │
│   ├── Benchmark.Cli/                                 # console host (assembly name: "benchmark")
│   │   Program.cs  →  list | run <id> | run --category <C> | run --all
│   │
│   └── UseCases/
│       ├── Benchmark.UseCases.Database.Abstractions/  # IDbProvider, BenchRow, InsertHelpers,
│       │                                              # the 7 steps, DbInsertUseCase  (dep-free)
│       ├── Benchmark.UseCases.Database.Postgres/      # Npgsql ONLY
│       └── Benchmark.UseCases.Database.Mssql/         # Microsoft.Data.SqlClient ONLY
│
├── tests/
│   ├── Benchmark.UnitTests/                          # fast Core tests + dep-free guards (no Docker)
│   └── Integration/                                  # Testcontainers DB benchmarks (need Docker)
├── .github/workflows/                                # ci.yml (PRs) + benchmarks.yml (scheduled)
├── Results/                                          # generated <id>.md + <id>.json (+ baseline/, _TEMPLATE.md)
├── Directory.Build.props  Directory.Packages.props  global.json   # shared settings · central pkg versions · pinned SDK
├── appsettings.json  appsettings.ci.json            # Run + per-domain (Database) config
└── AGENT.md  CLAUDE.md  README.md
```

**Data flow:** `Benchmark.Cli` builds a `UseCaseRegistry` (binding `appsettings.json`), resolves a use
case by id, and calls `IUseCase.RunAsync(HostContext)`. A `ChainedUseCase` runs its steps × variants
through `ChainRunner` (live console output) and returns a `UseCaseReport`; `MarkdownReporter` writes it
to `Results/<id>.md`. The report is engine-agnostic, so any future engine plugs into the same reporting.

---

## 5. How results are generated

- Every run produces a `UseCaseReport` (metadata + captured `EnvironmentInfo` + a step × variant matrix).
- `MarkdownReporter.WriteAsync` renders it to `Results/<Metadata.Id>.md`. **The file name is the use-case id.**
- Link each result from the catalog table in `README.md`.
- The reporter **overwrites** the whole file on each run, so don't hand-edit the numbers — re-run instead.
  The integration tests (`tests/Integration`) write their reports straight into `Results/` via
  `TestPaths.ResultsDir()`, so a Testcontainers run regenerates the committed file in the new format.
- To keep hand-written narrative (a "Findings" section), put it in `README.md`, or extend
  `MarkdownReporter` to preserve a block below a marker.

### Validation — machine-readable output + regression gate
- Each `run` also writes `Results/<id>.json` (BCL `System.Text.Json`, so Core stays dep-free) for
  history, diffing and CI ingestion. `JsonReporter` reads it back.
- `baseline <id>` promotes the latest `Results/<id>.json` to `Results/baseline/<id>.json` — commit that.
- `run <id> --gate` (or `compare <id>` on an existing result) compares medians to the baseline and
  **exits 2** if any metric regresses beyond `--threshold` percent (default 10), respecting
  `IsLowerBetter`. The matching/threshold logic is pure (`RegressionGate`) and covered by `JsonGateTests`.
- CI: `.github/workflows/ci.yml` builds + runs the Docker-free guards (`ArchitectureTests`, `JsonGateTests`)
  on every PR; `.github/workflows/benchmarks.yml` runs the Postgres benchmark + gate on a schedule/manually
  against a `postgres` service container (`appsettings.ci.json`). **Gate on consistent hardware** — shared
  runners vary, so the scheduled job uses a generous threshold.

---

## 6. Recipes

### 6.1 Add a step to the database use case
1. Create `src/UseCases/Benchmark.UseCases.Database.Abstractions/Steps/<Name>Step.cs` implementing
   `IBenchmarkStep` (copy an existing step like `BulkInsertStep.cs`). Read resources via the
   `UseCaseContext` extensions: `ctx.Provider()`, `ctx.Config()`, `ctx.IdGenerator()`.
2. Return `StepResult.Throughput(count, elapsed)` (or a custom `StepResult`). Return `null` to skip.
3. Add it to the `Steps` list in `DbInsertUseCase.cs`.

### 6.2 Add a new database engine (provider)
1. New project `src/UseCases/Benchmark.UseCases.Database.<Engine>/` referencing
   `Benchmark.UseCases.Database.Abstractions` **and the driver package** (the only place that driver appears).
2. Implement `IDbProvider` (model it on `PostgresProvider.cs`). `GetIndexSizeAsync` returns **megabytes**.
3. Add `<Engine>UuidInsertUseCase : DbInsertUseCase` with a `UseCaseMetadata` (unique `Id`) and
   `CreateProvider()` returning your provider.
4. Reference the new project from `Benchmark.Cli.csproj`, register it in `Program.cs → BuildRegistry`,
   and add a `Providers` entry under `Database` in `appsettings.json`.
5. Add it to the solution: `dotnet sln Benchmark.sln add --solution-folder src/UseCases <path>`.

### 6.3 Add a different engine (e.g. micro-benchmarks) — _future_
The framework is engine-agnostic but currently ships only the `Stopwatch` engine. To add, say,
statistically-rigorous micro-benchmarks for algorithms:
1. Create an **engine-adapter** project (e.g. `src/Engines/…`) that references the engine's package
   (e.g. BenchmarkDotNet) and `Benchmark.Core`. Give it a `UseCaseBase` that runs the engine and maps its
   output into a `UseCaseReport` (so the existing `MarkdownReporter` works unchanged).
2. Build use cases on that base with `Engine = BenchmarkEngine.BenchmarkDotNet`, then reference + register
   them in the CLI (as in 6.2 steps 4–5).
3. Keep the adapter **and its package** out of `Benchmark.Core` — they belong in their own project,
   exactly like a DB driver. The `BenchmarkEngine.BenchmarkDotNet` enum value already exists for this.

### 6.4 Add an entirely new domain (Stopwatch-style)
Create a project referencing only `Benchmark.Core` (+ whatever that domain needs). Write `IBenchmarkStep`s
and a `ChainedUseCase` (supply `Steps`, `Variants`, optional `SetupAsync`/`TeardownAsync`/`SeedContext`).
Use `Variant.Single` if there is nothing to compare. Register in the CLI.

---

## 7. Build · run · test

```bash
dotnet build Benchmark.sln -c Release

# list / run use cases (run from the repo root so ./appsettings.json and ./Results resolve)
dotnet run --project src/Benchmark.Cli -c Release -- list
dotnet run --project src/Benchmark.Cli -c Release -- run database.postgres.uuid-insert
dotnet run --project src/Benchmark.Cli -c Release -- run --category Database

# validation: set a baseline, then gate later runs against it
dotnet run --project src/Benchmark.Cli -c Release -- baseline database.postgres.uuid-insert
dotnet run --project src/Benchmark.Cli -c Release -- run database.postgres.uuid-insert --gate --threshold 10
dotnet run --project src/Benchmark.Cli -c Release -- compare database.postgres.uuid-insert   # gate without re-running

# fast tests — Core logic + dep-free guards + JSON/gate, no Docker:
dotnet test tests/Benchmark.UnitTests/Benchmark.UnitTests.csproj
# DB benchmarks — need Docker (Testcontainers):
dotnet test tests/Integration/Integration.csproj
dotnet test Benchmark.sln   # everything
```

Configuration lives in `appsettings.json`: a `Run` section (`Iterations`, `Warmup`) plus a per-domain
section (currently `Database`, with row counts and a `Providers` list). The CLI binds it; **Core never
sees configuration files**.

**Measurement rigor.** Each variant's chain runs `Warmup + Iterations` times (defaults `1 + 3`); only the
measured passes are aggregated and every cell reports the **median ± CV%** (coefficient of variation), so
you can tell stable numbers from noisy ones. `StopwatchEngine.MeasureAsync` forces a GC settle before each
timed section. Override per run with `--iterations <n> --warmup <n>`, or via the `Run` section. The whole
chain (not each step) is repeated, so stateful steps always start from the same freshly-rebuilt state.

---

## 8. Conventions

- **Never** add a third-party `PackageReference` to `Benchmark.Core` or `…Database.Abstractions` (see §3).
- **Committing is the user's responsibility** — don't `git commit`, amend, or push; leave changes in the
  working tree for the user to review. Only run git write operations if explicitly asked.
- One use case ⇒ one stable, lowercase, dotted `Id` (`category.engine.thing`) ⇒ one `Results/<id>.md` ⇒ one README catalog row.
- Steps stay portable: talk to `IDbProvider` + generic `System.Data.Common`, not a concrete driver.
- Keep `StepResult.Value` the comparable figure; set `IsLowerBetter` for size/latency metrics.
- Common build settings (`net9.0`, nullable, implicit usings) live in `Directory.Build.props`; **package
  versions go in `Directory.Packages.props`** (csproj reference packages with no `Version`); the SDK is
  pinned by `global.json`.
- Pure Core logic gets a fast unit test in `tests/Benchmark.UnitTests`; only Docker/DB work goes in `tests/Integration`.

### Test naming

Name every test with the **3-part** convention — PascalCase segments separated by underscores:

```
UnitUnderTest_Scenario_ExpectedBehavior
```

| Part | Meaning | Examples |
|---|---|---|
| **UnitUnderTest** | the method or type being exercised | `Compare`, `WriteAsync`, `From`, `Core` |
| **Scenario** | the input or state under test | `ThroughputDropBeyondThreshold`, `SingleSample`, `OnRegeneration` |
| **ExpectedBehavior** | the asserted outcome | `FlagsRegression`, `HasZeroSpread`, `PreservesNotesBelowMarker` |

- ✅ `Compare_ThroughputDropBeyondThreshold_FlagsRegression`
- ✅ `WriteAsync_OnRegeneration_PreservesNotesBelowMarker`
- ❌ `gate_flags_a_drop` / `TestCompare` / `Compare_works` — no scenario+expectation, or not 3 parts.

Keep one behavior per test (one logical assertion theme); if a name needs "And", consider splitting it.

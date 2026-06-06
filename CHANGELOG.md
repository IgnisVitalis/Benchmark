# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project aims to follow [Semantic Versioning](https://semver.org/).

## [0.1.0] — 2026-06-06

First structured release: the repo was reshaped from a DB-only benchmark script into a tech-agnostic
performance-estimation framework with a dependency-free core, measurement rigor, a validation/regression
gate, CI, and tests.

### Added
- **Dependency-free `Benchmark.Core`** — abstractions (`IBenchmarkStep`, `IUseCase`, `UseCaseContext`,
  `Variant`, `StepResult`, `StepStats`, `UseCaseReport`, `IRunLog`), `ChainRunner` + `ChainedUseCase`,
  `StopwatchEngine`, reporting (`ConsoleReporter`, `MarkdownReporter`, `JsonReporter`, `RegressionGate`,
  `ReportFormat`, `NumberFormat`) and hosting (`HostContext`, `UseCaseRegistry`). References only `System.*`.
- **`Benchmark.Cli`** host (`benchmark`) with commands `list`, `run <id>`, `run --category`, `run --all`,
  `baseline <id>`, `compare <id>`, and options `--iterations/--warmup/--gate/--threshold/--config/--results`.
- **Per-use-case projects** carrying their own dependencies: `Database.Abstractions` (dep-free `IDbProvider`
  + the seven insert/read steps + `DbInsertUseCase`), `Database.Postgres` (Npgsql), `Database.Mssql`
  (Microsoft.Data.SqlClient).
- **Measurement rigor** — warm-up + N iterations (the whole chain is repeated), aggregated to
  **median ± CV%** (`StepStats`), with a GC settle before each timed section and Debug/debugger warnings.
- **Variants** — run the same chain over different data/config with automatic gain (Δ%) vs the first
  (baseline) variant; generalised from the old hard-coded `Guid.NewGuid()` → `Guid v7` rounds.
- **Unified reporting** — a bordered console table and a Markdown report with identical content; plus a
  machine-readable `Results/<id>.json`.
- **Regression gate** — `baseline`/`compare`/`run --gate` compare medians to a committed baseline and exit
  non-zero past `--threshold` percent (respecting `IsLowerBetter`).
- **CI** — `.github/workflows/ci.yml` (build + Docker-free tests on every PR) and `benchmarks.yml`
  (Postgres service + gate, scheduled/manual) with `appsettings.ci.json`.
- **Tests** — `Benchmark.UnitTests` (fast, Docker-free: number formatting, stats, table rendering,
  registry, the dep-free architecture guards, and JSON/gate logic) alongside the Testcontainers
  `Integration` benchmarks.
- **Repo hygiene** — `Directory.Build.props` (shared TFM/nullable/usings), `Directory.Packages.props`
  (central package versions), `global.json` (pinned SDK), `.editorconfig`.
- **Docs** — `AGENT.md` (canonical guide + recipes), `CLAUDE.md` (Claude/Windows notes), catalog-style
  `README.md`, `Results/_TEMPLATE.md`, and this changelog.
- **Markdown notes preservation** — content below the `<!-- notes:keep-below -->` marker survives report
  regeneration, so hand-written analysis is not overwritten.
- **Cancellation** — Ctrl+C is honoured between steps and within the per-row insert/lookup loops.

### Changed
- Reporting values are now **culture-invariant**, so generated reports are byte-identical across locales.
- `IDbProvider.GetIndexSizeAsync` returns megabytes (`double`) instead of a pre-formatted string.
- Results are **harness-generated** (`Results/<id>.{md,json}`) and linked from the README catalog, rather
  than hand-pasted into the README. The integration tests write straight into `Results/`.

### Removed
- The exploratory **Algorithms / BenchmarkDotNet** example use case and engine adapter (kept the framework
  DB-only). The `BenchmarkEngine.BenchmarkDotNet` enum value remains as the documented hook for a future
  micro-benchmark engine.
- `Microsoft.Extensions.Logging` from the core, replaced by the tiny BCL-only `IRunLog` seam.

[0.1.0]: https://example.com/your-repo/releases/tag/v0.1.0

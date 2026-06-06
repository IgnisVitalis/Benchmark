# CLAUDE.md

Operational notes for Claude Code in this repo. **`AGENT.md` is the source of truth** for architecture,
vocabulary, and the recipes — read it first. This file only adds Claude-/Windows-specific specifics.

## The one rule you must not break

`Benchmark.Core` and `Benchmark.UseCases.Database.Abstractions` reference **only `System.*` (the BCL)**.
**Never** add a NuGet `PackageReference` to either. Domain dependencies (Npgsql, Microsoft.Data.SqlClient,
BenchmarkDotNet, …) go **only** in the leaf use-case / engine projects. `tests/Benchmark.UnitTests/ArchitectureTests.cs`
fails if you break this — run it after touching project references.

## Committing is the user's job

**Do not `git commit`, amend, or push.** Leave all changes in the working tree for the user to review and
commit themselves. Only run git write operations if the user explicitly asks for them in that request.

## Environment

- Windows + **PowerShell** (`powershell.exe`, 5.1). No `&&`/`||` chaining, no `head`/`tail`/`grep` — use the
  dedicated Read/Grep/Glob tools and PowerShell equivalents.
- .NET SDK 9 (`net9.0`). The CLI assembly is named `benchmark`.
- The DB integration tests need **Docker** (Testcontainers). The architecture tests do not.

## Commands

```powershell
dotnet build Benchmark.sln -c Release

# run from the repo root so ./appsettings.json and ./Results resolve
dotnet run --project src/Benchmark.Cli -c Release -- list
dotnet run --project src/Benchmark.Cli -c Release -- run <id>            # e.g. database.postgres.uuid-insert
dotnet run --project src/Benchmark.Cli -c Release -- run --category Database

# architecture guards only (fast, no Docker):
dotnet test tests/Integration/Integration.csproj --filter "FullyQualifiedName~ArchitectureTests"
```

## Where things live

- Abstractions / runner / reporters → `src/Benchmark.Core/`
- DB steps, `IDbProvider`, `DbInsertUseCase` → `src/UseCases/Benchmark.UseCases.Database.Abstractions/`
- Concrete DB engines → `…Database.Postgres` (Npgsql), `…Database.Mssql` (SqlClient)
- Host / arg parsing / registry → `src/Benchmark.Cli/Program.cs`
- Generated reports → `Results/<use-case-id>.md` (one file per use case; the id is the file name)

## Conventions

- Adding a use case, step, or DB provider → follow the recipes in `AGENT.md §6`, then register it in
  `Program.cs`, reference it from `Benchmark.Cli.csproj`, and add it to the README catalog + the solution
  (`dotnet sln Benchmark.sln add --solution-folder … <path>`).
- Prefer extending the framework over special-casing. Keep steps portable (talk to `IDbProvider`, not a driver).
- Don't hand-edit generated `Results/*.md` numbers — re-run the use case instead.
- Name tests with the 3-part convention `MethodUnderTest_Scenario_ExpectedBehavior` — full rule + examples in `AGENT.md §8`.

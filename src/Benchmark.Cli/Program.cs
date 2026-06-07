using Benchmark.Core;
using Benchmark.UseCases.Database;
using Benchmark.UseCases.Database.Mssql;
using Benchmark.UseCases.Database.Postgres;
using Benchmark.UseCases.Database.DeviceViews;
using Benchmark.UseCases.Database.DeviceViews.Postgres;
using Benchmark.UseCases.Database.DeviceViews.Mongo;
using Microsoft.Extensions.Configuration;

// ── Benchmark.Cli — the performance use-case runner ───────────────────────────
// Usage:
//   list                       List available use cases
//   run <id>                   Run one use case and write Results/<id>.md
//   run --category <Category>  Run every use case in a category
//   run --all                  Run every use case
// Options: --config <path> (default appsettings.json), --results <dir> (default ./Results)

Console.OutputEncoding = System.Text.Encoding.UTF8;   // so Δ% (and any Unicode) render on Windows
var log = new ConsoleRunLog();

#if DEBUG
log.Line("WARNING: Debug build — measurements are unreliable; prefer `dotnet run -c Release`.");
#endif
if (System.Diagnostics.Debugger.IsAttached)
    log.Line("WARNING: a debugger is attached — timings will be skewed.");

string? command    = args.Length > 0 ? args[0].ToLowerInvariant() : null;
string  configPath = GetOption("--config")  ?? "appsettings.json";
string  resultsDir = GetOption("--results") ?? Path.Combine(Directory.GetCurrentDirectory(), "Results");
string? category   = GetOption("--category");
bool    runAll     = args.Contains("--all");

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile(configPath, optional: true)
    .Build();

var registry = BuildRegistry(config, log);

int iterations = int.TryParse(GetOption("--iterations"), out var itv) ? itv : config.GetValue<int?>("Run:Iterations") ?? 3;
int warmup     = int.TryParse(GetOption("--warmup"),     out var wuv) ? wuv : config.GetValue<int?>("Run:Warmup")     ?? 1;

bool   gate        = args.Contains("--gate");
double threshold   = double.TryParse(GetOption("--threshold"), out var thv) ? thv : 10.0;
string baselineDir = Path.Combine(resultsDir, "baseline");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
var host = new HostContext(log, cts.Token, iterations, warmup);

int exit;
switch (command)
{
    case "list":
        PrintCatalog();
        exit = 0;
        break;

    case "run":
        exit = await RunAsync();
        break;

    case "baseline":
        exit = Baseline();
        break;

    case "compare":
        exit = await CompareReports();
        break;

    default:
        PrintUsage();
        exit = command is null ? 0 : 1;
        break;
}
return exit;

// ── commands ──────────────────────────────────────────────────────────────────

async Task<int> RunAsync()
{
    IReadOnlyList<IUseCase> targets;

    if (runAll)
        targets = registry.Catalog.Select(m => registry.Resolve(m.Id)!).ToList();
    else if (category is not null)
        targets = registry.ResolveCategory(category);
    else
    {
        var id = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
        if (id is null)
        {
            log.Line("Usage: run <id> | run --category <Category> | run --all");
            return 1;
        }
        var uc = registry.Resolve(id);
        if (uc is null)
        {
            log.Line($"Unknown use case '{id}'.");
            log.Line("");
            PrintCatalog();
            return 1;
        }
        targets = [uc];
    }

    if (targets.Count == 0)
    {
        log.Line("No matching use cases. Check your appsettings.json (enabled providers) or category name.");
        return 1;
    }

    bool regressed = false;
    foreach (var uc in targets)
    {
        log.Line("");
        log.Line($"===== {uc.Metadata.Title}  ({uc.Metadata.Id}) =====");
        try
        {
            var report = await uc.RunAsync(host);
            var md      = await MarkdownReporter.WriteAsync(report, resultsDir, host.Ct);
            var json    = await JsonReporter.WriteAsync(report, resultsDir, host.Ct);
            var compact = await CompactReporter.WriteAsync(report, resultsDir, host.Ct);
            log.Line($"→ {md}");
            log.Line($"→ {json}");
            log.Line($"→ {compact}");

            if (gate)
                regressed |= await GateAsync(report);
        }
        catch (OperationCanceledException)
        {
            log.Line("Cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            log.Line($"FAILED: {ex.Message}");
            return 1;
        }
    }
    return regressed ? 2 : 0;   // exit 2 = a metric regressed beyond the threshold
}

// Compares a fresh report to the committed baseline; returns true if anything regressed.
async Task<bool> GateAsync(UseCaseReport current)
{
    var path = Path.Combine(baselineDir, current.Metadata.Id + ".json");
    if (!File.Exists(path))
    {
        log.Line($"  gate: no baseline at {path} — run `baseline {current.Metadata.Id}` to set one.");
        return false;
    }

    var baseline = await JsonReporter.ReadFileAsync(path, host.Ct);
    var result   = RegressionGate.Compare(current, baseline, threshold);

    if (!result.HasRegressions)
    {
        log.Line($"  gate: PASS — no metric regressed beyond {result.ThresholdPct:F0}% vs baseline.");
        return false;
    }

    log.Line($"  gate: FAIL — {result.Regressions.Count} regression(s) beyond {result.ThresholdPct:F0}%:");
    foreach (var r in result.Regressions)
        log.Line($"    {r.Step} [{r.Variant}]: {NumberFormat.Value(r.BaselineMedian)} → " +
                 $"{NumberFormat.Value(r.CurrentMedian)}  ({r.ImprovePct:+0.0;-0.0}%)");
    return true;
}

// Gates an already-produced Results/<id>.json against the baseline, without re-running.
async Task<int> CompareReports()
{
    var id = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
    if (id is null) { log.Line("Usage: compare <id> [--threshold <pct>]"); return 1; }

    var path = Path.Combine(resultsDir, id + ".json");
    if (!File.Exists(path)) { log.Line($"  no result at {path} — run `run {id}` first."); return 1; }

    var current   = await JsonReporter.ReadFileAsync(path, host.Ct);
    bool regressed = await GateAsync(current);
    return regressed ? 2 : 0;
}

// Promotes the latest Results/<id>.json to the committed baseline.
int Baseline()
{
    var ids = args.Skip(1).Where(a => !a.StartsWith("--")).ToList();
    if (ids.Count == 0)
    {
        log.Line("Usage: baseline <id> [<id> …]   (promotes Results/<id>.json to the committed baseline)");
        return 1;
    }

    Directory.CreateDirectory(baselineDir);
    foreach (var id in ids)
    {
        var src = Path.Combine(resultsDir, id + ".json");
        if (!File.Exists(src))
        {
            log.Line($"  no result at {src} — run `run {id}` first.");
            return 1;
        }
        var dst = Path.Combine(baselineDir, id + ".json");
        File.Copy(src, dst, overwrite: true);
        log.Line($"  baseline set: {dst}");
    }
    return 0;
}

// ── helpers ───────────────────────────────────────────────────────────────────

UseCaseRegistry BuildRegistry(IConfiguration cfg, IRunLog logger)
{
    var registry = new UseCaseRegistry();

    var db = cfg.GetSection("Database").Get<DatabaseConfig>() ?? new DatabaseConfig();
    foreach (var p in db.Providers.Where(x => x.Enabled))
    {
        switch (p.Type.ToUpperInvariant())
        {
            case "POSTGRESQL":
                registry.Register(() => new PostgresUuidInsertUseCase(db, p.ConnStr, p.AdminConnStr));
                break;
            case "MSSQL":
                registry.Register(() => new MssqlUuidInsertUseCase(db, p.ConnStr, p.AdminConnStr));
                break;
            default:
                logger.Line($"(skipping unknown provider type '{p.Type}')");
                break;
        }
    }

    // Device storage-views comparison. Postgres (relational + JSONB) is required; MongoDB is added as a
    // third representation when a Mongo connection string is also configured.
    var dv = cfg.GetSection("DeviceViews").Get<DeviceViewsConfig>();
    if (dv is not null && !string.IsNullOrWhiteSpace(dv.ConnStr))
        registry.Register(() =>
        {
            var stores = new List<NamedStore>
            {
                new("Relational", new PostgresRelationalDeviceStore(dv.ConnStr)),
                new("JSONB",      new PostgresJsonbDeviceStore(dv.ConnStr)),
            };
            if (!string.IsNullOrWhiteSpace(dv.MongoConnStr))
                stores.Add(new NamedStore("MongoDB", new MongoDeviceStore(dv.MongoConnStr)));
            return new DeviceViewsUseCase(dv, stores);
        });

    return registry;
}

void PrintCatalog()
{
    log.Line("Available use cases:");
    log.Line("");
    log.Line($"  {"Id",-34} {"Category",-12} {"Engine",-16} Title");
    log.Line("  " + new string('-', 92));
    foreach (var m in registry.Catalog)
        log.Line($"  {m.Id,-34} {m.Category,-12} {m.Engine,-16} {m.Title}");
    log.Line("");
    log.Line("Run one with:  dotnet run --project src/Benchmark.Cli -- run <id>");
}

void PrintUsage()
{
    log.Line("Benchmark — performance use-case runner");
    log.Line("");
    log.Line("Commands:");
    log.Line("  list                       List available use cases");
    log.Line("  run <id>                   Run one use case; writes Results/<id>.md + .json");
    log.Line("  run --category <Category>  Run every use case in a category");
    log.Line("  run --all                  Run every use case");
    log.Line("  baseline <id>              Promote the latest Results/<id>.json to the committed baseline");
    log.Line("  compare <id>               Gate the latest Results/<id>.json vs baseline; exit 2 on regression");
    log.Line("");
    log.Line("Options:");
    log.Line("  --config <path>            appsettings.json path (default: ./appsettings.json)");
    log.Line("  --results <dir>            Output directory (default: ./Results)");
    log.Line("  --iterations <n>           Measured passes per variant (default: 3)");
    log.Line("  --warmup <n>               Warm-up passes, discarded (default: 1)");
    log.Line("  --gate                     After run, compare to baseline; exit 2 on regression");
    log.Line("  --threshold <pct>          Regression threshold % for --gate (default: 10)");
}

string? GetOption(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

using System.Reflection;
using Benchmark.Core;
using Benchmark.UseCases.Database;
using Benchmark.UseCases.Database.DeviceViews;

/// <summary>
/// Guards the framework's central invariant: the Core (and the DB abstraction layer) stay free of any
/// third-party technology. Pure reflection — no Docker.
/// </summary>
public class ArchitectureTests
{
    [Fact]
    public void Core_ReferencedAssemblies_AreBclOnly()
    {
        var offenders = NonBclReferences(typeof(IUseCase).Assembly);
        Assert.True(offenders.Count == 0,
            "Benchmark.Core must reference only System.* assemblies. Offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void DatabaseAbstractions_ReferencedAssemblies_HaveNoDbDriver()
    {
        var refs = typeof(IDbProvider).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        Assert.DoesNotContain("Npgsql", refs);
        Assert.DoesNotContain("Microsoft.Data.SqlClient", refs);
        Assert.DoesNotContain("BenchmarkDotNet", refs);
    }

    [Fact]
    public void DeviceViewsAbstractions_ReferencedAssemblies_HaveNoDbDriver()
    {
        var refs = typeof(IDeviceStore).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        Assert.DoesNotContain("Npgsql", refs);
        Assert.DoesNotContain("MongoDB.Driver", refs);
    }

    private static List<string> NonBclReferences(Assembly asm) =>
        asm.GetReferencedAssemblies()
           .Select(a => a.Name!)
           .Where(n => !n.StartsWith("System", StringComparison.Ordinal)
                    && n != "netstandard"
                    && n != "mscorlib")
           .ToList();
}

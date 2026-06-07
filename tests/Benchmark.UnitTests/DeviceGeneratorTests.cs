using Benchmark.UseCases.Database.DeviceViews;

/// <summary>The generator must be deterministic so every storage variant inserts identical data.</summary>
public class DeviceGeneratorTests
{
    [Fact]
    public void Generate_CalledTwice_ProducesIdenticalData()
    {
        var a = DeviceGenerator.Generate(200);
        var b = DeviceGenerator.Generate(200);

        Assert.Equal(200, a.Length);
        Assert.Equal(a, b);   // Device is a record → structural equality across all 20 fields
    }

    [Fact]
    public void Generate_AnyCount_ProducesUniqueUuids()
    {
        var devices = DeviceGenerator.Generate(1000);

        Assert.Equal(1000, devices.Select(d => d.Uuid).Distinct().Count());
    }
}

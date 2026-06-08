using Benchmark.UseCases.Database.DeviceViews;

/// <summary>The generator must be deterministic per index, so every storage variant inserts identical data
/// and a streamed 100M run is reproducible without holding the set in memory.</summary>
public class DeviceGeneratorTests
{
    [Fact]
    public void Generate_SameIndex_ProducesIdenticalDevice()
    {
        Assert.Equal(DeviceGenerator.Generate(123), DeviceGenerator.Generate(123));   // record → structural equality
    }

    [Fact]
    public void Generate_DifferentIndices_ProduceDifferentDevices()
    {
        Assert.NotEqual(DeviceGenerator.Generate(1), DeviceGenerator.Generate(2));
    }

    [Fact]
    public void Stream_CalledTwice_ProducesIdenticalData()
    {
        Assert.Equal(DeviceGenerator.Stream(200).ToArray(), DeviceGenerator.Stream(200).ToArray());
    }

    [Fact]
    public void Stream_AnyCount_ProducesUniqueUuids()
    {
        var devices = DeviceGenerator.Stream(1000).ToArray();

        Assert.Equal(1000, devices.Select(d => d.Uuid).Distinct().Count());
    }
}

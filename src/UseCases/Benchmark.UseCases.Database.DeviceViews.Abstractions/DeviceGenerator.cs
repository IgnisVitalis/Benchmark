namespace Benchmark.UseCases.Database.DeviceViews;

/// <summary>
/// Generates a <em>deterministic</em> device dataset where every device is fully determined by its
/// <c>index</c> alone (a per-index RNG seed, not a running sequence). Two consequences matter:
/// <list type="bullet">
///   <item>The same index always yields the same device — so every storage variant inserts identical
///         data, and a re-run reproduces it exactly.</item>
///   <item>Any index can be generated on its own, so the set <see cref="Stream"/>s in constant memory —
///         essential for 100M-device runs that can't be held in RAM.</item>
/// </list>
/// </summary>
public static class DeviceGenerator
{
    private static readonly string[] Types         = ["Sensor", "Gateway", "Camera", "Thermostat", "Lock", "Light", "Hub", "Meter"];
    private static readonly string[] Manufacturers = ["Acme", "Globex", "Initech", "Umbrella", "Soylent", "Hooli"];
    private static readonly string[] Statuses      = ["Online", "Offline", "Degraded", "Maintenance"];
    private static readonly DateTimeOffset LastBase = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RegBase  = new(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>One device, fully determined by its <paramref name="index"/>.</summary>
    public static Device Generate(int index)
    {
        var rng = new Random(SeedFor(index));   // per-index seed → reproducible and order-independent
        return new Device(
            Uuid:             DeterministicGuid(index),
            Name:             $"device-{index:D9}",
            Type:             Types[rng.Next(Types.Length)],
            Manufacturer:     Manufacturers[rng.Next(Manufacturers.Length)],
            Model:            $"M{rng.Next(1000):D4}",                  // ~1000 values → selective enough to use an index
            SerialNumber:     $"SN-{index:D9}-{rng.Next(9999):D4}",
            FirmwareVersion:  $"{rng.Next(1, 6)}.{rng.Next(20)}.{rng.Next(50)}",
            HardwareRevision: $"rev{rng.Next(1, 10)}",
            MacAddress:       Mac(rng),
            IpAddress:        $"10.{rng.Next(256)}.{rng.Next(256)}.{rng.Next(256)}",
            Location:         $"room-{rng.Next(500)}",
            Latitude:         Math.Round(rng.NextDouble() * 180 - 90, 6),
            Longitude:        Math.Round(rng.NextDouble() * 360 - 180, 6),
            Status:           Statuses[rng.Next(Statuses.Length)],
            BatteryLevel:     rng.Next(101),
            SignalStrength:   -rng.Next(30, 120),
            IsActive:         rng.Next(2) == 1,
            LastSeen:         LastBase.AddSeconds(rng.Next(31_536_000)),
            RegisteredAt:     RegBase.AddSeconds(rng.Next(31_536_000)),
            Description:      $"Device {index} — extended descriptive text to give the payload a realistic size.");
    }

    /// <summary>Lazily streams the first <paramref name="count"/> devices (constant memory).</summary>
    public static IEnumerable<Device> Stream(int count)
    {
        for (int i = 0; i < count; i++)
            yield return Generate(i);
    }

    // Stable multiplicative-mix hash of the index → a well-spread, runtime-independent seed.
    // (HashCode.Combine is randomized per process, so it can't be used here.)
    private static int SeedFor(int index)
    {
        ulong x = (uint)index * 0x9E3779B97F4A7C15UL;
        x ^= x >> 29;
        return unchecked((int)x);
    }

    private static Guid DeterministicGuid(int i)
    {
        Span<byte> b = stackalloc byte[16];
        BitConverter.TryWriteBytes(b, i);
        for (int k = 4; k < 16; k++) b[k] = (byte)(i * 31 + k * 7);
        return new Guid(b);
    }

    private static string Mac(Random rng) =>
        string.Join(":", Enumerable.Range(0, 6).Select(_ => rng.Next(256).ToString("X2")));
}

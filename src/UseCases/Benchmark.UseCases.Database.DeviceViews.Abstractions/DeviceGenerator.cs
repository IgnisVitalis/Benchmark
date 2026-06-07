namespace Benchmark.UseCases.Database.DeviceViews;

/// <summary>
/// Generates a <em>deterministic</em> device dataset: the same <c>count</c> devices on every call (fixed
/// seed reset each time, UUIDs derived from the index). That guarantees every variant inserts identical
/// data, so the only difference between representations is the representation itself.
/// </summary>
public static class DeviceGenerator
{
    private static readonly string[] Types         = ["Sensor", "Gateway", "Camera", "Thermostat", "Lock", "Light", "Hub", "Meter"];
    private static readonly string[] Manufacturers = ["Acme", "Globex", "Initech", "Umbrella", "Soylent", "Hooli"];
    private static readonly string[] Statuses      = ["Online", "Offline", "Degraded", "Maintenance"];

    public static Device[] Generate(int count)
    {
        var rng     = new Random(20240601);   // fresh, fixed seed every call → reproducible
        var devices = new Device[count];

        var lastBase = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var regBase  = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);

        for (int i = 0; i < count; i++)
        {
            devices[i] = new Device(
                Uuid:             DeterministicGuid(i),
                Name:             $"device-{i:D8}",
                Type:             Types[rng.Next(Types.Length)],
                Manufacturer:     Manufacturers[rng.Next(Manufacturers.Length)],
                Model:            $"M{rng.Next(1000):D4}",
                SerialNumber:     $"SN-{i:D8}-{rng.Next(9999):D4}",
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
                LastSeen:         lastBase.AddSeconds(rng.Next(31_536_000)),
                RegisteredAt:     regBase.AddSeconds(rng.Next(31_536_000)),
                Description:      $"Device {i} — extended descriptive text to give the payload a realistic size.");
        }
        return devices;
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

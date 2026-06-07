namespace Benchmark.UseCases.Database.DeviceViews;

/// <summary>A device-info object with 20 mixed-type properties — the unit stored three different ways.</summary>
public sealed record Device(
    Guid           Uuid,             // unique primary key (indexed)
    string         Name,
    string         Type,            // indexed, non-unique
    string         Manufacturer,
    string         Model,
    string         SerialNumber,    // NOT indexed (the "no index" lookup)
    string         FirmwareVersion,
    string         HardwareRevision,
    string         MacAddress,
    string         IpAddress,
    string         Location,
    double         Latitude,
    double         Longitude,
    string         Status,
    int            BatteryLevel,    // NOT indexed (range/filter query)
    int            SignalStrength,
    bool           IsActive,
    DateTimeOffset LastSeen,        // updated by UpdateLastSeen
    DateTimeOffset RegisteredAt,
    string         Description);

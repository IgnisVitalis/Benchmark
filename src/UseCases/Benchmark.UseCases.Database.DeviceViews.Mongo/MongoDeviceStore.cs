using System.Text;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Benchmark.UseCases.Database.DeviceViews.Mongo;

/// <summary>
/// Devices as MongoDB documents. The uuid is mapped to <c>_id</c> (Mongo's automatic unique index — the
/// equivalent of the relational PK); <c>type</c> gets a secondary index; serial and battery are unindexed,
/// so those queries scan the collection. Field names are snake_case so the document shape matches the
/// Postgres JSONB representation. The Device record stays attribute-free (it lives in the dependency-free
/// Abstractions project) — mapping is configured here via conventions + a programmatic class map.
/// </summary>
public sealed class MongoDeviceStore : IDeviceStore
{
    private const string DbName    = "devicebench";
    private const string ColName   = "devices";
    private const int    BatchSize = 50_000;   // insert in batches so a streamed 100M load stays bounded

    private readonly IMongoClient _client;
    private readonly IMongoCollection<Device> _col;

    static MongoDeviceStore()
    {
        // Store Guids as standard UUID binary (driver 3.x has no implicit default).
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        // snake_case element names → same document shape as the JSONB store (uuid → _id is the exception).
        ConventionRegistry.Register("device-snake-case",
            new ConventionPack { new SnakeCaseElementNameConvention() }, t => t == typeof(Device));

        if (!BsonClassMap.IsClassMapRegistered(typeof(Device)))
            BsonClassMap.RegisterClassMap<Device>(cm =>
            {
                cm.AutoMap();
                cm.MapIdMember(d => d.Uuid);   // uuid → _id (unique by default)
            });
    }

    public MongoDeviceStore(string connStr)
    {
        _client = new MongoClient(connStr);
        _col    = _client.GetDatabase(DbName).GetCollection<Device>(ColName);
    }

    public string Name => "MongoDB";

    public async Task ResetAsync(CancellationToken ct)
    {
        var db = _client.GetDatabase(DbName);
        await db.DropCollectionAsync(ColName, ct);
        await db.CreateCollectionAsync(ColName, cancellationToken: ct);
        // Secondary index on Model before load, so it is maintained during insert (matches the SQL stores).
        await _col.Indexes.CreateOneAsync(
            new CreateIndexModel<Device>(Builders<Device>.IndexKeys.Ascending(d => d.Model)),
            cancellationToken: ct);
    }

    public async Task InsertManyAsync(IEnumerable<Device> devices, CancellationToken ct)
    {
        var opts = new InsertManyOptions { IsOrdered = false };
        foreach (var batch in devices.Chunk(BatchSize))   // Chunk pulls one batch at a time → constant memory
            await _col.InsertManyAsync(batch, opts, ct);
    }

    // The lookups fetch the matching uuids (projected to _id) — the same workload as the SQL stores'
    // "SELECT uuid … [LIMIT n]" — rather than a server-side count.

    public async Task<long> FindByUuidAsync(IReadOnlyList<Guid> uuids, int repeats, CancellationToken ct)
    {
        long found = 0;
        for (int i = 0; i < repeats; i++)
        {
            ct.ThrowIfCancellationRequested();
            var filter = Builders<Device>.Filter.Eq(d => d.Uuid, uuids[i % uuids.Count]);   // _id index
            found += (await _col.Find(filter).Project(d => d.Uuid).ToListAsync(ct)).Count;
        }
        return found;
    }

    public async Task<long> FindByModelAsync(IReadOnlyList<string> models, int repeats, int limit, CancellationToken ct)
    {
        long found = 0;
        for (int i = 0; i < repeats; i++)
        {
            ct.ThrowIfCancellationRequested();
            var filter = Builders<Device>.Filter.Eq(d => d.Model, models[i % models.Count]);   // Model index
            found += (await _col.Find(filter).Limit(limit).Project(d => d.Uuid).ToListAsync(ct)).Count;
        }
        return found;
    }

    public async Task<long> FindBySerialAsync(IReadOnlyList<string> serials, int repeats, CancellationToken ct)
    {
        long found = 0;
        for (int i = 0; i < repeats; i++)
        {
            ct.ThrowIfCancellationRequested();
            var filter = Builders<Device>.Filter.Eq(d => d.SerialNumber, serials[i % serials.Count]);  // no index → scan
            found += (await _col.Find(filter).Project(d => d.Uuid).ToListAsync(ct)).Count;
        }
        return found;
    }

    public async Task<long> UpdateLastSeenAsync(IReadOnlyList<Guid> uuids, int repeats, DateTimeOffset lastSeen, CancellationToken ct)
    {
        long affected = 0;
        var update = Builders<Device>.Update.Set(d => d.LastSeen, lastSeen);
        for (int i = 0; i < repeats; i++)
        {
            ct.ThrowIfCancellationRequested();
            var filter = Builders<Device>.Filter.Eq(d => d.Uuid, uuids[i % uuids.Count]);
            var result = await _col.UpdateOneAsync(filter, update, cancellationToken: ct);
            affected += result.ModifiedCount;
        }
        return affected;
    }

    public async Task<long> RangeByBatteryAsync(int maxBattery, int repeats, int limit, CancellationToken ct)
    {
        long found = 0;
        var filter = Builders<Device>.Filter.Lt(d => d.BatteryLevel, maxBattery);   // no index → scan
        for (int i = 0; i < repeats; i++)
        {
            ct.ThrowIfCancellationRequested();
            found += (await _col.Find(filter).Limit(limit).Project(d => d.Uuid).ToListAsync(ct)).Count;
        }
        return found;
    }

    public async Task<double> StorageSizeMbAsync(CancellationToken ct)
    {
        var pipeline = new[]
        {
            new BsonDocument("$collStats", new BsonDocument("storageStats", new BsonDocument())),
        };
        var doc = await (await _col.AggregateAsync<BsonDocument>(pipeline, cancellationToken: ct)).FirstAsync(ct);
        var s = doc["storageStats"].AsBsonDocument;
        // Use the logical document size ("size"), not WiredTiger's "storageSize": the latter is
        // compressed and checkpoint-dependent (reads ~0 right after a bulk insert). Logical size is also
        // more comparable to Postgres, which doesn't compress these small rows.
        double bytes = s["size"].ToDouble() + s.GetValue("totalIndexSize", 0).ToDouble();
        return bytes / 1024.0 / 1024.0;
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>Maps C# member names to snake_case BSON element names (so Mongo docs match the JSONB shape).</summary>
internal sealed class SnakeCaseElementNameConvention : ConventionBase, IMemberMapConvention
{
    public void Apply(BsonMemberMap memberMap) => memberMap.SetElementName(ToSnakeCase(memberMap.MemberName));

    private static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsUpper(c) && i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}

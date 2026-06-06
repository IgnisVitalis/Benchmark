namespace Benchmark.Core;

/// <summary>
/// Typed state bag threaded through the steps of one variant run. Generalises the old DB-specific
/// BenchmarkContext: domain code stores and reads its own entries via <see cref="Set{T}"/> /
/// <see cref="Get{T}"/>, keeping Core agnostic of any particular technology. A fresh context is
/// created per variant, so state never leaks between variants.
/// </summary>
public sealed class UseCaseContext
{
    private readonly Dictionary<Type, object> _items = new();

    /// <summary>The label of the variant currently running (e.g. "Guid v7").</summary>
    public required string VariantLabel { get; init; }

    public required IRunLog Log { get; init; }

    public CancellationToken Ct { get; init; }

    /// <summary>Publishes a value into the bag, keyed by its static type <typeparamref name="T"/>.</summary>
    public void Set<T>(T value) where T : notnull => _items[typeof(T)] = value;

    /// <summary>Reads a previously published value, throwing if it was never set.</summary>
    public T Get<T>() =>
        _items.TryGetValue(typeof(T), out var v)
            ? (T)v
            : throw new InvalidOperationException(
                $"No context item of type '{typeof(T).Name}' was set. " +
                "Publish it from the use case's SeedContext or an earlier step.");

    /// <summary>Reads a value if present.</summary>
    public bool TryGet<T>(out T value)
    {
        if (_items.TryGetValue(typeof(T), out var v)) { value = (T)v; return true; }
        value = default!;
        return false;
    }
}

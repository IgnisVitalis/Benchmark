namespace Benchmark.Core;

/// <summary>
/// In-memory catalog of available use cases. The host registers <em>factories</em> (so a fresh,
/// un-mutated instance is built for each run) and looks them up by <see cref="UseCaseMetadata.Id"/>.
/// </summary>
public sealed class UseCaseRegistry
{
    private readonly List<(UseCaseMetadata Meta, Func<IUseCase> Factory)> _entries = [];

    /// <summary>Registers a factory; probes it once to capture metadata for the catalog.</summary>
    public UseCaseRegistry Register(Func<IUseCase> factory)
    {
        _entries.Add((factory().Metadata, factory));
        return this;
    }

    /// <summary>All registered metadata, ordered by id.</summary>
    public IReadOnlyList<UseCaseMetadata> Catalog =>
        _entries.Select(e => e.Meta)
                .OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

    /// <summary>Builds a fresh instance for the given id, or <c>null</c> if unknown.</summary>
    public IUseCase? Resolve(string id) =>
        _entries.FirstOrDefault(e => string.Equals(e.Meta.Id, id, StringComparison.OrdinalIgnoreCase))
                .Factory?.Invoke();

    /// <summary>Builds fresh instances for every use case in a category.</summary>
    public IReadOnlyList<IUseCase> ResolveCategory(string category) =>
        _entries.Where(e => string.Equals(e.Meta.Category, category, StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Factory())
                .ToList();
}

using Benchmark.Core;

namespace Benchmark.UseCases.Database;

/// <summary>
/// Typed accessors for the DB items published into the shared <see cref="UseCaseContext"/>.
/// The use case seeds <see cref="IDbProvider"/> and <see cref="DatabaseConfig"/>; each variant seeds
/// the id generator; steps read them back and pass sampled ids forward — that hand-off is the chain.
/// </summary>
public static class DbContextExtensions
{
    public static IDbProvider    Provider(this UseCaseContext c)     => c.Get<IDbProvider>();
    public static DatabaseConfig Config(this UseCaseContext c)       => c.Get<DatabaseConfig>();
    public static Func<Guid>     IdGenerator(this UseCaseContext c)  => c.Get<Func<Guid>>();

    public static Guid[] SampledIds(this UseCaseContext c)           => c.TryGet<Guid[]>(out var ids) ? ids : [];
    public static void   SetSampledIds(this UseCaseContext c, Guid[] ids) => c.Set(ids);
}

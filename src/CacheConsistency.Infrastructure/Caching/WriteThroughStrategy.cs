using CacheConsistency.Core.Abstractions;
using CacheConsistency.Core.Models;
using StackExchange.Redis;

namespace CacheConsistency.Infrastructure.Caching;

public sealed class WriteThroughStrategy : CacheStrategyBase
{
    public WriteThroughStrategy(IProductRepository repo, IDatabase cache) : base(repo, cache) { }

    public override string Name => "Write-Through (write DB then cache, same path)";

    public override async Task<Product?> GetAsync(int id, CancellationToken ct = default)
    {
        var cached = Deserialize(await Cache.StringGetAsync(Key(id)));
        if (cached is not null)
        {
            RecordHit();
            return cached;
        }

        RecordMiss();
        var fromDb = await Repo.GetByIdAsync(id, ct);
        if (fromDb is not null)
            await Cache.StringSetAsync(Key(id), Serialize(fromDb));
        return fromDb;
    }

    public override async Task<Product?> UpdateStockAsync(
        int id, int newStock, long expectedVersion, CancellationToken ct = default)
    {
        var updated = await Repo.TryUpdateStockAsync(id, newStock, expectedVersion, ct);
        if (updated is not null)
            await Cache.StringSetAsync(Key(id), Serialize(updated));
        return updated;
    }
}

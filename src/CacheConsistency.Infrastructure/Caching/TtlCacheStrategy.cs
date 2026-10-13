using CacheConsistency.Core.Abstractions;
using CacheConsistency.Core.Models;
using StackExchange.Redis;

namespace CacheConsistency.Infrastructure.Caching;

public sealed class TtlCacheStrategy : CacheStrategyBase
{
    private readonly TimeSpan _ttl;

    public TtlCacheStrategy(IProductRepository repo, IDatabase cache, TimeSpan ttl)
        : base(repo, cache) => _ttl = ttl;

    public override string Name => $"TTL ({_ttl.TotalSeconds:0}s expiry, no write invalidation)";

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
            await Cache.StringSetAsync(Key(id), Serialize(fromDb), _ttl);
        return fromDb;
    }

    public override async Task<Product?> UpdateStockAsync(
        int id, int newStock, long expectedVersion, CancellationToken ct = default)
    {
        return await Repo.TryUpdateStockAsync(id, newStock, expectedVersion, ct);
    }
}

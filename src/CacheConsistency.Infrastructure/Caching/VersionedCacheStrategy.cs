using CacheConsistency.Core.Abstractions;
using CacheConsistency.Core.Models;
using StackExchange.Redis;

namespace CacheConsistency.Infrastructure.Caching;

public sealed class VersionedCacheStrategy : CacheStrategyBase
{
    private const string VersionedSetScript = @"
        local raw = redis.call('GET', KEYS[1])
        if raw then
            local ok, existing = pcall(cjson.decode, raw)
            if ok and existing['Version'] and tonumber(existing['Version']) >= tonumber(ARGV[2]) then
                return 0
            end
        end
        local ttl = tonumber(ARGV[3])
        if ttl and ttl > 0 then
            redis.call('SET', KEYS[1], ARGV[1], 'EX', ttl)
        else
            redis.call('SET', KEYS[1], ARGV[1])
        end
        return 1";

    public VersionedCacheStrategy(IProductRepository repo, IDatabase cache, CacheRecoveryOptions? recovery = null)
        : base(repo, cache, recovery) { }

    public override string Name => "Versioned Write-Through (monotonic, version-guarded)";

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
            await VersionedSetAsync(fromDb);
        return fromDb;
    }

    public override async Task<Product?> UpdateStockAsync(
        int id, int newStock, long expectedVersion, CancellationToken ct = default)
    {
        var updated = await Repo.TryUpdateStockAsync(id, newStock, expectedVersion, ct);
        if (updated is not null)
            await VersionedSetAsync(updated);
        return updated;
    }

    public async Task<bool> VersionedSetAsync(Product p)
    {
        var ttlSeconds = Recovery.SafetyTtl is { } ttl ? (long)ttl.TotalSeconds : 0L;
        try
        {
            var result = await Cache.ScriptEvaluateAsync(
                VersionedSetScript,
                new RedisKey[] { Key(p.Id) },
                new RedisValue[] { Serialize(p), p.Version, ttlSeconds });
            return (long)result == 1;
        }
        catch when (Recovery.Enabled)
        {
            await DeleteQuietlyAsync(p.Id);
            return false;
        }
    }
}

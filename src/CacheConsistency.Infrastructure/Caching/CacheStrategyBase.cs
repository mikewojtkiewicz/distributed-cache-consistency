using System.Text.Json;
using CacheConsistency.Core.Abstractions;
using CacheConsistency.Core.Models;
using StackExchange.Redis;

namespace CacheConsistency.Infrastructure.Caching;

public abstract class CacheStrategyBase : ICacheStrategy
{
    protected readonly IProductRepository Repo;
    protected readonly IDatabase Cache;
    protected readonly CacheRecoveryOptions Recovery;

    private long _hits;
    private long _misses;

    protected CacheStrategyBase(IProductRepository repo, IDatabase cache, CacheRecoveryOptions? recovery = null)
    {
        Repo = repo;
        Cache = cache;
        Recovery = recovery ?? CacheRecoveryOptions.Disabled;
    }

    public abstract string Name { get; }

    public long CacheHits => Interlocked.Read(ref _hits);
    public long CacheMisses => Interlocked.Read(ref _misses);

    protected void RecordHit() => Interlocked.Increment(ref _hits);
    protected void RecordMiss() => Interlocked.Increment(ref _misses);

    protected static string Key(int id) => $"product:{id}";

    protected static string Serialize(Product p) => JsonSerializer.Serialize(p);

    protected static Product? Deserialize(RedisValue v) =>
        v.IsNullOrEmpty ? null : JsonSerializer.Deserialize<Product>(v!);

    protected Task CacheSetAsync(Product p) =>
        Recovery.SafetyTtl is { } ttl
            ? Cache.StringSetAsync(Key(p.Id), Serialize(p), ttl)
            : Cache.StringSetAsync(Key(p.Id), Serialize(p));

    protected async Task PopulateAsync(Product p)
    {
        try
        {
            await CacheSetAsync(p);
        }
        catch when (Recovery.Enabled)
        {
        }
    }

    protected async Task WriteThroughAsync(Product p)
    {
        try
        {
            await CacheSetAsync(p);
        }
        catch when (Recovery.Enabled)
        {
            await DeleteQuietlyAsync(p.Id);
        }
    }

    protected async Task InvalidateAsync(int id)
    {
        try
        {
            await Cache.KeyDeleteAsync(Key(id));
        }
        catch when (Recovery.Enabled)
        {
        }
    }

    protected async Task DeleteQuietlyAsync(int id)
    {
        try
        {
            await Cache.KeyDeleteAsync(Key(id));
        }
        catch
        {
        }
    }

    public abstract Task<Product?> GetAsync(int id, CancellationToken ct = default);

    public abstract Task<Product?> UpdateStockAsync(
        int id, int newStock, long expectedVersion, CancellationToken ct = default);

    public virtual Task ResetAsync(CancellationToken ct = default)
    {
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
        return Task.CompletedTask;
    }
}

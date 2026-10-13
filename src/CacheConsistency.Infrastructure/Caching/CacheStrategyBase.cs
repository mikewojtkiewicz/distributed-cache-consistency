using System.Text.Json;
using CacheConsistency.Core.Abstractions;
using CacheConsistency.Core.Models;
using StackExchange.Redis;

namespace CacheConsistency.Infrastructure.Caching;

public abstract class CacheStrategyBase : ICacheStrategy
{
    protected readonly IProductRepository Repo;
    protected readonly IDatabase Cache;

    private long _hits;
    private long _misses;

    protected CacheStrategyBase(IProductRepository repo, IDatabase cache)
    {
        Repo = repo;
        Cache = cache;
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

using CacheConsistency.Core.Abstractions;
using CacheConsistency.Infrastructure.Caching;
using StackExchange.Redis;

namespace CacheConsistency.Application;

public static class StrategyFactory
{
    public static readonly string[] Names = { "cache-aside", "ttl", "write-through", "versioned" };

    public static ICacheStrategy Create(
        string name, IProductRepository repo, IDatabase cache, TimeSpan? ttl = null) =>
        name.ToLowerInvariant() switch
        {
            "cache-aside" or "aside" => new CacheAsideStrategy(repo, cache),
            "ttl"                    => new TtlCacheStrategy(repo, cache, ttl ?? TimeSpan.FromSeconds(30)),
            "write-through" or "wt"  => new WriteThroughStrategy(repo, cache),
            "versioned" or "vwt"     => new VersionedCacheStrategy(repo, cache),
            _ => throw new ArgumentException(
                     $"unknown strategy '{name}'. options: {string.Join(", ", Names)}"),
        };
}

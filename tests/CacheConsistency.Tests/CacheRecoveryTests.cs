using System.Text.Json;
using CacheConsistency.Core.Models;
using CacheConsistency.Infrastructure.Caching;
using Xunit;

namespace CacheConsistency.Tests;

public class CacheRecoveryTests
{
    private static Product Seed() =>
        new() { Id = 1, Name = "Widget", Stock = 100, PriceCents = 1999, Version = 1 };

    private static readonly CacheRecoveryOptions Recovery =
        new(true, TimeSpan.FromSeconds(30));

    [Fact]
    public async Task Write_through_with_recovery_invalidates_on_cache_failure_and_avoids_a_stale_read()
    {
        var repo = new InMemoryProductRepository(Seed());
        var (database, cache) = InMemoryRedisDatabase.Create();
        var strategy = new WriteThroughStrategy(repo, database, Recovery);
        await database.StringSetAsync("product:1", JsonSerializer.Serialize(Seed()));
        cache.FailNext("StringSetAsync");

        var updated = await strategy.UpdateStockAsync(1, 90, 1);

        Assert.NotNull(updated);
        Assert.Equal(2, updated!.Version);
        Assert.Equal(2, (await repo.GetByIdAsync(1))!.Version);
        // Recovery deleted the stale key, so the next read reloads fresh from the database.
        Assert.Equal(2, (await strategy.GetAsync(1))!.Version);
    }

    [Fact]
    public async Task Versioned_with_recovery_invalidates_on_cache_failure_and_avoids_a_stale_read()
    {
        var repo = new InMemoryProductRepository(Seed());
        var (database, cache) = InMemoryRedisDatabase.Create();
        var strategy = new VersionedCacheStrategy(repo, database, Recovery);
        await database.StringSetAsync("product:1", JsonSerializer.Serialize(Seed()));
        cache.FailNext("ScriptEvaluateAsync");

        var updated = await strategy.UpdateStockAsync(1, 90, 1);

        Assert.NotNull(updated);
        Assert.Equal(2, updated!.Version);
        Assert.Equal(2, (await strategy.GetAsync(1))!.Version);
    }

    [Fact]
    public async Task Cache_aside_with_recovery_keeps_the_write_committed_when_invalidation_fails()
    {
        var repo = new InMemoryProductRepository(Seed());
        var (database, cache) = InMemoryRedisDatabase.Create();
        var strategy = new CacheAsideStrategy(repo, database, Recovery);
        await database.StringSetAsync("product:1", JsonSerializer.Serialize(Seed()));
        cache.FailNext("KeyDeleteAsync");

        // Without recovery this throws; with recovery the committed write is returned and the
        // failed invalidation is swallowed (the stale entry is bounded by the safety TTL).
        var updated = await strategy.UpdateStockAsync(1, 90, 1);

        Assert.NotNull(updated);
        Assert.Equal(2, updated!.Version);
        Assert.Equal(2, (await repo.GetByIdAsync(1))!.Version);
    }
}

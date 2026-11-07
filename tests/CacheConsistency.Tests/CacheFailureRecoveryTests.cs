using System.Text.Json;
using CacheConsistency.Core.Models;
using CacheConsistency.Infrastructure.Caching;
using StackExchange.Redis;
using Xunit;

namespace CacheConsistency.Tests;

public class CacheFailureRecoveryTests
{
    private static Product Seed() =>
        new() { Id = 1, Name = "Widget", Stock = 100, PriceCents = 1999, Version = 1 };

    [Fact]
    public async Task Write_through_recovers_after_a_failed_cache_update()
    {
        var repo = new InMemoryProductRepository(Seed());
        var (database, cache) = InMemoryRedisDatabase.Create();
        var strategy = new WriteThroughStrategy(repo, database);
        await database.StringSetAsync("product:1", JsonSerializer.Serialize(Seed()));
        cache.FailNext("StringSetAsync");

        await Assert.ThrowsAsync<RedisException>(() => strategy.UpdateStockAsync(1, 90, 1));
        Assert.Equal(2, (await repo.GetByIdAsync(1))!.Version);
        Assert.Equal(1, (await strategy.GetAsync(1))!.Version);

        var recovered = await strategy.UpdateStockAsync(1, 80, 2);

        Assert.Equal(3, recovered!.Version);
        Assert.Equal(3, (await strategy.GetAsync(1))!.Version);
    }

    [Fact]
    public async Task Cache_aside_recovers_after_a_failed_invalidation()
    {
        var repo = new InMemoryProductRepository(Seed());
        var (database, cache) = InMemoryRedisDatabase.Create();
        var strategy = new CacheAsideStrategy(repo, database);
        await database.StringSetAsync("product:1", JsonSerializer.Serialize(Seed()));
        cache.FailNext("KeyDeleteAsync");

        await Assert.ThrowsAsync<RedisException>(() => strategy.UpdateStockAsync(1, 90, 1));
        Assert.Equal(2, (await repo.GetByIdAsync(1))!.Version);
        Assert.Equal(1, (await strategy.GetAsync(1))!.Version);

        var recovered = await strategy.UpdateStockAsync(1, 80, 2);

        Assert.Equal(3, recovered!.Version);
        Assert.Equal(3, (await strategy.GetAsync(1))!.Version);
    }
}

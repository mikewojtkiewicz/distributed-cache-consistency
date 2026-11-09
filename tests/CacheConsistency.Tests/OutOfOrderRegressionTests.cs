using System.Text.Json;
using CacheConsistency.Core.Models;
using CacheConsistency.Infrastructure.Caching;
using Xunit;

namespace CacheConsistency.Tests;

public class OutOfOrderRegressionTests
{
    private static Product Seed() =>
        new() { Id = 1, Name = "Widget", Stock = 100, PriceCents = 1999, Version = 1 };

    [Fact]
    public async Task Plain_write_through_regresses_when_cache_writes_arrive_out_of_order()
    {
        var (database, _) = InMemoryRedisDatabase.Create();
        var newer = Seed() with { Stock = 150, Version = 5 };
        var older = Seed() with { Stock = 120, Version = 4 };

        // Simulate the race: the newer write lands first, the slower older write lands last.
        await database.StringSetAsync("product:1", JsonSerializer.Serialize(newer));
        await database.StringSetAsync("product:1", JsonSerializer.Serialize(older));

        var cached = JsonSerializer.Deserialize<Product>((await database.StringGetAsync("product:1"))!)!;
        Assert.Equal(4, cached.Version); // regressed: the cache moved backwards
    }

    [Fact]
    public async Task Versioned_write_through_rejects_the_stale_write_and_does_not_regress()
    {
        var repo = new InMemoryProductRepository(Seed());
        var (database, _) = InMemoryRedisDatabase.Create();
        var strategy = new VersionedCacheStrategy(repo, database);
        var newer = Seed() with { Stock = 150, Version = 5 };
        var older = Seed() with { Stock = 120, Version = 4 };

        await strategy.VersionedSetAsync(newer);
        var acceptedOlder = await strategy.VersionedSetAsync(older);

        Assert.False(acceptedOlder); // the version guard rejects the stale write
        var cached = JsonSerializer.Deserialize<Product>((await database.StringGetAsync("product:1"))!)!;
        Assert.Equal(5, cached.Version); // cache stays at the newest committed version
    }
}

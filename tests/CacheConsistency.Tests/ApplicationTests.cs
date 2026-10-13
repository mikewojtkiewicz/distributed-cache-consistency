using CacheConsistency.Application;
using CacheConsistency.Core.Models;
using CacheConsistency.Infrastructure.Caching;
using Xunit;

namespace CacheConsistency.Tests;

public class ApplicationTests
{
    private static Product Seed() =>
        new() { Id = 1, Name = "Widget", Stock = 100, PriceCents = 1999, Version = 1 };

    [Theory]
    [InlineData("cache-aside", typeof(CacheAsideStrategy))]
    [InlineData("ttl", typeof(TtlCacheStrategy))]
    [InlineData("write-through", typeof(WriteThroughStrategy))]
    [InlineData("versioned", typeof(VersionedCacheStrategy))]
    public void StrategyFactory_creates_the_requested_type(string name, Type expected)
    {
        var strategy = StrategyFactory.Create(name, new InMemoryProductRepository(Seed()), cache: null!);
        Assert.IsType(expected, strategy);
    }

    [Fact]
    public void StrategyFactory_rejects_an_unknown_name()
    {
        Assert.Throws<ArgumentException>(() =>
            StrategyFactory.Create("nope", new InMemoryProductRepository(Seed()), cache: null!));
    }

    [Fact]
    public async Task Runner_SetStock_persists_the_new_value_and_bumps_version()
    {
        var repo = new InMemoryProductRepository(Seed());
        var runner = new CacheRunner(new PassthroughCacheStrategy(repo), repo);

        var updated = await runner.SetStockAsync(1, 73);

        Assert.NotNull(updated);
        Assert.Equal(73, updated!.Stock);
        Assert.Equal(2, updated.Version);
    }

    [Fact]
    public async Task Runner_Adjust_applies_a_relative_delta()
    {
        var repo = new InMemoryProductRepository(Seed());
        var runner = new CacheRunner(new PassthroughCacheStrategy(repo), repo);

        var updated = await runner.AdjustStockAsync(1, -10);

        Assert.NotNull(updated);
        Assert.Equal(90, updated!.Stock);
    }
}

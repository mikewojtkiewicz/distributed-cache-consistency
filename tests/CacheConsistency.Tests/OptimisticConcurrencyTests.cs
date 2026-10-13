using CacheConsistency.Core.Abstractions;
using CacheConsistency.Core.Models;
using Xunit;

namespace CacheConsistency.Tests;

public class OptimisticConcurrencyTests
{
    private static Product Seed() =>
        new() { Id = 1, Name = "Widget", Stock = 100, PriceCents = 1999, Version = 1 };

    [Fact]
    public async Task TryUpdate_succeeds_on_matching_version_and_bumps_it()
    {
        IProductRepository repo = new InMemoryProductRepository(Seed());

        var before = await repo.GetByIdAsync(1);
        var after = await repo.TryUpdateStockAsync(1, 42, before!.Version);

        Assert.NotNull(after);
        Assert.Equal(42, after!.Stock);
        Assert.Equal(before.Version + 1, after.Version);
    }

    [Fact]
    public async Task TryUpdate_rejects_a_stale_version()
    {
        IProductRepository repo = new InMemoryProductRepository(Seed());
        var current = await repo.GetByIdAsync(1);

        var firstWriter = await repo.TryUpdateStockAsync(1, 99, current!.Version);
        Assert.NotNull(firstWriter);

        var secondWriterWithStaleVersion = await repo.TryUpdateStockAsync(1, 98, current.Version);
        Assert.Null(secondWriterWithStaleVersion);
    }

    [Fact]
    public async Task Concurrent_decrements_converge_with_no_lost_updates()
    {
        const int writers = 50;
        IProductRepository repo = new InMemoryProductRepository(Seed());
        var start = (await repo.GetByIdAsync(1))!.Stock;

        var tasks = Enumerable.Range(0, writers).Select(_ => Task.Run(async () =>
        {
            while (true)
            {
                var current = await repo.GetByIdAsync(1);
                var updated = await repo.TryUpdateStockAsync(1, current!.Stock - 1, current.Version);
                if (updated is not null)
                    return;
            }
        }));
        await Task.WhenAll(tasks);

        var end = await repo.GetByIdAsync(1);
        Assert.Equal(start - writers, end!.Stock);
        Assert.Equal(1 + writers, end.Version);
    }
}

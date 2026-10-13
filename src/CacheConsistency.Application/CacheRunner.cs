using CacheConsistency.Core.Abstractions;
using CacheConsistency.Core.Models;

namespace CacheConsistency.Application;

public sealed class CacheRunner
{
    private readonly ICacheStrategy _strategy;
    private readonly IProductRepository _repo;

    public CacheRunner(ICacheStrategy strategy, IProductRepository repo)
    {
        _strategy = strategy;
        _repo = repo;
    }

    public string StrategyName => _strategy.Name;
    public long Hits => _strategy.CacheHits;
    public long Misses => _strategy.CacheMisses;

    public Task<Product?> GetAsync(int id, CancellationToken ct = default) =>
        _strategy.GetAsync(id, ct);

    public async Task<Product?> SetStockAsync(
        int id, int newStock, int maxAttempts = 5, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var current = await _repo.GetByIdAsync(id, ct);
            if (current is null)
                return null;

            var updated = await _strategy.UpdateStockAsync(id, newStock, current.Version, ct);
            if (updated is not null)
                return updated;
        }
        return null;
    }

    public async Task<Product?> AdjustStockAsync(
        int id, int delta, int maxAttempts = 10, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var current = await _repo.GetByIdAsync(id, ct);
            if (current is null)
                return null;

            var updated = await _strategy.UpdateStockAsync(id, current.Stock + delta, current.Version, ct);
            if (updated is not null)
                return updated;
        }
        return null;
    }
}

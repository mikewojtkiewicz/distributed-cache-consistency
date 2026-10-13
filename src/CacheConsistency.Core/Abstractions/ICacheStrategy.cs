using CacheConsistency.Core.Models;

namespace CacheConsistency.Core.Abstractions;

public interface ICacheStrategy
{
    string Name { get; }

    long CacheHits { get; }
    long CacheMisses { get; }

    Task<Product?> GetAsync(int id, CancellationToken ct = default);

    Task<Product?> UpdateStockAsync(
        int id, int newStock, long expectedVersion, CancellationToken ct = default);

    Task ResetAsync(CancellationToken ct = default);
}

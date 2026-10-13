using CacheConsistency.Core.Models;

namespace CacheConsistency.Core.Abstractions;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<Product?> TryUpdateStockAsync(
        int id, int newStock, long expectedVersion, CancellationToken ct = default);
}

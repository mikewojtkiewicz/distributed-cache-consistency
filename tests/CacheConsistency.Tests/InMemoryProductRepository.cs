using CacheConsistency.Core.Abstractions;
using CacheConsistency.Core.Models;

namespace CacheConsistency.Tests;

public sealed class InMemoryProductRepository : IProductRepository
{
    private readonly object _gate = new();
    private Product _product;

    public InMemoryProductRepository(Product seed) => _product = seed;

    public Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult<Product?>(_product.Id == id ? _product : null);
    }

    public Task<Product?> TryUpdateStockAsync(
        int id, int newStock, long expectedVersion, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_product.Id != id || _product.Version != expectedVersion)
                return Task.FromResult<Product?>(null);

            _product = _product with { Stock = newStock, Version = _product.Version + 1 };
            return Task.FromResult<Product?>(_product);
        }
    }
}

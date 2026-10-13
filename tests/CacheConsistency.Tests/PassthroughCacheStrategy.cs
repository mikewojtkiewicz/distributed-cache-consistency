using CacheConsistency.Core.Abstractions;
using CacheConsistency.Core.Models;

namespace CacheConsistency.Tests;

public sealed class PassthroughCacheStrategy : ICacheStrategy
{
    private readonly IProductRepository _repo;

    public PassthroughCacheStrategy(IProductRepository repo) => _repo = repo;

    public string Name => "Passthrough (test)";
    public long CacheHits => 0;
    public long CacheMisses => 0;

    public Task<Product?> GetAsync(int id, CancellationToken ct = default) =>
        _repo.GetByIdAsync(id, ct);

    public Task<Product?> UpdateStockAsync(
        int id, int newStock, long expectedVersion, CancellationToken ct = default) =>
        _repo.TryUpdateStockAsync(id, newStock, expectedVersion, ct);

    public Task ResetAsync(CancellationToken ct = default) => Task.CompletedTask;
}

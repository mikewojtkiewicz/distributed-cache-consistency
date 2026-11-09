using CacheConsistency.Core.Abstractions;
using CacheConsistency.Core.Models;

namespace CacheConsistency.Benchmarks;

public sealed class FaultInjectingProductRepository : IProductRepository
{
    private readonly IProductRepository _inner;
    private readonly WorkloadOptions _options;
    private int _enabled;
    private long _readFailuresInjected;
    private long _writeFailuresInjected;

    public FaultInjectingProductRepository(IProductRepository inner, WorkloadOptions options)
    {
        _inner = inner;
        _options = options;
    }

    public void Enable() => Volatile.Write(ref _enabled, 1);
    public void Disable() => Volatile.Write(ref _enabled, 0);

    public (long ReadFailures, long WriteFailures) Snapshot() =>
        (Interlocked.Read(ref _readFailuresInjected), Interlocked.Read(ref _writeFailuresInjected));

    public Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        MaybeFail(isRead: true);
        return _inner.GetByIdAsync(id, ct);
    }

    public Task<Product?> TryUpdateStockAsync(
        int id, int newStock, long expectedVersion, CancellationToken ct = default)
    {
        MaybeFail(isRead: false);
        return _inner.TryUpdateStockAsync(id, newStock, expectedVersion, ct);
    }

    private void MaybeFail(bool isRead)
    {
        if (Volatile.Read(ref _enabled) == 0)
            return;

        var configuredRate = isRead ? _options.DatabaseReadFailureRate : _options.DatabaseWriteFailureRate;
        if (Random.Shared.NextDouble() >= Math.Max(_options.DatabaseFailureRate, configuredRate))
            return;

        if (isRead)
            Interlocked.Increment(ref _readFailuresInjected);
        else
            Interlocked.Increment(ref _writeFailuresInjected);
        throw new InvalidOperationException(isRead
            ? "Injected database read failure."
            : "Injected database write failure.");
    }
}

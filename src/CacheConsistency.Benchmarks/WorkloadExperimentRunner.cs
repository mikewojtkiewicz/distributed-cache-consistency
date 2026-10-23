using System.Diagnostics;
using CacheConsistency.Core.Abstractions;
using CacheConsistency.Core.Models;
using CacheConsistency.Infrastructure.Caching;
using CacheConsistency.Infrastructure.Data;
using StackExchange.Redis;

namespace CacheConsistency.Benchmarks;

public sealed class WorkloadExperimentRunner
{
    private readonly ProductRepository _repository;
    private readonly IDatabase _redis;

    public WorkloadExperimentRunner(ProductRepository repository, IDatabase redis)
    {
        _repository = repository;
        _redis = redis;
    }

    public async Task<IReadOnlyList<ExperimentResult>> RunAllAsync(
        WorkloadOptions options,
        CancellationToken ct = default)
    {
        options.Validate();
        var results = new List<ExperimentResult>();

        var strategies = new (string Label, Func<IDatabase, ICacheStrategy> Build)[]
        {
            ("CacheAside", db => new CacheAsideStrategy(_repository, db)),
            ("TTL", db => new TtlCacheStrategy(_repository, db, TimeSpan.FromSeconds(options.TtlSeconds))),
            ("WriteThrough", db => new WriteThroughStrategy(_repository, db)),
            ("Versioned", db => new VersionedCacheStrategy(_repository, db))
        };

        foreach (var definition in strategies)
        {
            ct.ThrowIfCancellationRequested();
            Console.WriteLine();
            Console.WriteLine($"Running {definition.Label}: {options.Workers} workers, " +
                              $"{options.ReadPercent}/{options.WritePercent} read/write, {options.Duration.TotalSeconds:0.##}s...");

            // Flush with the raw connection so injected cache faults cannot break experiment setup.
            await _redis.ExecuteAsync("FLUSHDB");
            var (proxiedRedis, faultController) = FaultInjectingRedisDatabase.Create(_redis, options);
            var strategy = definition.Build(proxiedRedis);
            await strategy.ResetAsync(ct);

            var metrics = new WorkloadMetrics();
            faultController.Enable();
            var timer = Stopwatch.StartNew();
            using var durationCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            durationCts.CancelAfter(options.Duration);

            var workers = Enumerable.Range(0, options.Workers)
                .Select(_ => WorkerAsync(strategy, options, metrics, durationCts.Token))
                .ToArray();

            try
            {
                await Task.WhenAll(workers);
            }
            catch (OperationCanceledException) when (durationCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                // Expected when the configured experiment duration expires.
            }
            finally
            {
                faultController.Disable();
                timer.Stop();
            }

            ct.ThrowIfCancellationRequested();
            var result = metrics.BuildResult(
                strategy.Name,
                options,
                timer.Elapsed,
                strategy.CacheHits,
                strategy.CacheMisses,
                faultController.Snapshot());
            results.Add(result);
            PrintResult(result);
        }

        return results;
    }

    private async Task WorkerAsync(
        ICacheStrategy strategy,
        WorkloadOptions options,
        WorkloadMetrics metrics,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (Random.Shared.Next(100) < options.ReadPercent)
                    await ReadOnceAsync(strategy, options, metrics, ct);
                else
                    await WriteOnceAsync(strategy, options, metrics, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ReadOnceAsync(
        ICacheStrategy strategy,
        WorkloadOptions options,
        WorkloadMetrics metrics,
        CancellationToken ct)
    {
        metrics.ReadStarted();
        var started = Stopwatch.GetTimestamp();
        var latencyRecorded = false;
        Product? value;
        try
        {
            value = await strategy.GetAsync(options.ProductId, ct);
            metrics.RecordReadLatency(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            latencyRecorded = true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            metrics.ReadFailed();
            return;
        }
        finally
        {
            if (!latencyRecorded)
                metrics.RecordReadLatency(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        if (value is null)
        {
            metrics.ReadFailed();
            return;
        }

        metrics.ReadSucceeded();
        if (!options.VerifyReadsAgainstDatabase)
            return;

        // This source-of-truth query is deliberately outside the measured strategy-read latency.
        // If validation itself fails, retain the successful-read count but skip stale classification.
        Product? sourceOfTruth;
        try
        {
            sourceOfTruth = await _repository.GetByIdAsync(options.ProductId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            metrics.ValidationFailed();
            return;
        }

        if (sourceOfTruth is null)
        {
            metrics.ValidationFailed();
            return;
        }

        metrics.ReadValidated();
        if (value.Version < sourceOfTruth.Version)
            metrics.StaleRead();
        else if (value.Version > sourceOfTruth.Version)
            metrics.CacheAheadRead();
    }

    private async Task WriteOnceAsync(
        ICacheStrategy strategy,
        WorkloadOptions options,
        WorkloadMetrics metrics,
        CancellationToken ct)
    {
        metrics.WriteStarted();
        var started = Stopwatch.GetTimestamp();
        try
        {
            var current = await strategy.GetAsync(options.ProductId, ct)
                          ?? await _repository.GetByIdAsync(options.ProductId, ct);
            if (current is null)
            {
                metrics.WriteFailed();
                return;
            }

            var delta = Random.Shared.Next(2) == 0 ? -1 : 1;
            for (var attempt = 0; attempt <= options.MaxWriteRetries; attempt++)
            {
                var newStock = Math.Max(0, current.Stock + delta);
                var updated = await strategy.UpdateStockAsync(
                    options.ProductId,
                    newStock,
                    current.Version,
                    ct);

                if (updated is not null)
                {
                    metrics.WriteCommitted();
                    return;
                }

                metrics.WriteConflict();
                if (attempt >= options.MaxWriteRetries)
                    break;

                metrics.WriteRetry();
                // Get the source-of-truth version after an optimistic-concurrency conflict. This
                // keeps a stale cache value from causing an infinite retry loop.
                current = await _repository.GetByIdAsync(options.ProductId, ct);
                if (current is null)
                    break;
            }

            metrics.WriteFailed();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // A Redis/cache exception can occur after the database commit. Therefore this is
            // counted as an operation error, not proof that the database write was rolled back.
            metrics.WriteFailed();
        }
        finally
        {
            metrics.RecordWriteLatency(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private static void PrintResult(ExperimentResult r)
    {
        var staleRate = r.ValidatedReads == 0 ? 0 : 100.0 * r.StaleReads / r.ValidatedReads;
        Console.WriteLine($"  elapsed             : {r.ElapsedSeconds:F2}s");
        Console.WriteLine($"  operations          : reads={r.ReadOperations:N0} (ok={r.SuccessfulReads:N0}, failed={r.ReadFailures:N0}), " +
                          $"writes={r.WriteOperations:N0} (committed={r.CommittedWrites:N0}, failed={r.FailedWrites:N0})");
        Console.WriteLine($"  stale reads         : {r.StaleReads:N0}/{r.ValidatedReads:N0} validated ({staleRate:F3}%), " +
                          $"validation failures={r.ValidationFailures:N0}, ahead-of-DB={r.CacheAheadReads:N0}");
        Console.WriteLine($"  latency read ms     : p50={r.ReadLatencyMs.P50Ms:F3}, p95={r.ReadLatencyMs.P95Ms:F3}, p99={r.ReadLatencyMs.P99Ms:F3}");
        Console.WriteLine($"  latency write ms    : p50={r.WriteLatencyMs.P50Ms:F3}, p95={r.WriteLatencyMs.P95Ms:F3}, p99={r.WriteLatencyMs.P99Ms:F3}");
        Console.WriteLine($"  throughput          : {r.OperationsPerSecond:F1} logical ops/s");
        Console.WriteLine($"  cache               : hits={r.CacheHits:N0}, misses={r.CacheMisses:N0}");
        Console.WriteLine($"  concurrency         : conflicts={r.WriteConflicts:N0}, retries={r.WriteRetries:N0}");
        Console.WriteLine($"  injected conditions : cache failures={r.CacheFailuresInjected:N0}, failed invalidations={r.InvalidationsFailed:N0}, " +
                          $"delays={r.ArtificialDelaysApplied:N0}, extra reorder delays={r.OutOfOrderDelaysApplied:N0}");
    }
}

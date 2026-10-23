using System.Collections.Concurrent;
using System.Diagnostics;

namespace CacheConsistency.Benchmarks;

public sealed record LatencyPercentiles(
    int Samples,
    double MeanMs,
    double P50Ms,
    double P95Ms,
    double P99Ms);

public sealed record ExperimentResult(
    string Strategy,
    int Workers,
    int ReadPercent,
    int WritePercent,
    double ElapsedSeconds,
    long ReadOperations,
    long SuccessfulReads,
    long ValidatedReads,
    long ValidationFailures,
    long StaleReads,
    long CacheAheadReads,
    long ReadFailures,
    long WriteOperations,
    long CommittedWrites,
    long FailedWrites,
    long WriteConflicts,
    long WriteRetries,
    long CacheHits,
    long CacheMisses,
    long CacheFailuresInjected,
    long InvalidationsFailed,
    long ArtificialDelaysApplied,
    long OutOfOrderDelaysApplied,
    double OperationsPerSecond,
    LatencyPercentiles ReadLatencyMs,
    LatencyPercentiles WriteLatencyMs);

internal sealed class LatencySampler
{
    private const int Capacity = 100_000;
    private readonly long[] _samples = new long[Capacity];
    private long _seen;

    public void Record(double milliseconds)
    {
        var seen = Interlocked.Increment(ref _seen);
        long slot;
        if (seen <= Capacity)
        {
            slot = seen - 1;
        }
        else
        {
            // Reservoir sampling keeps the sample representative without unbounded memory growth.
            var candidate = Random.Shared.NextInt64(seen);
            if (candidate >= Capacity)
                return;
            slot = candidate;
        }

        Interlocked.Exchange(ref _samples[(int)slot], BitConverter.DoubleToInt64Bits(milliseconds));
    }

    public LatencyPercentiles Summarize()
    {
        var count = (int)Math.Min(Interlocked.Read(ref _seen), Capacity);
        if (count == 0)
            return new LatencyPercentiles(0, 0, 0, 0, 0);

        var values = new double[count];
        for (var i = 0; i < count; i++)
            values[i] = BitConverter.Int64BitsToDouble(Interlocked.Read(ref _samples[i]));

        Array.Sort(values);
        var mean = values.Average();
        return new LatencyPercentiles(
            count,
            mean,
            Percentile(values, 0.50),
            Percentile(values, 0.95),
            Percentile(values, 0.99));
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        var index = Math.Clamp((int)Math.Ceiling(percentile * sorted.Length) - 1, 0, sorted.Length - 1);
        return sorted[index];
    }
}

internal sealed class WorkloadMetrics
{
    private long _readOperations;
    private long _successfulReads;
    private long _validatedReads;
    private long _validationFailures;
    private long _staleReads;
    private long _cacheAheadReads;
    private long _readFailures;
    private long _writeOperations;
    private long _committedWrites;
    private long _failedWrites;
    private long _writeConflicts;
    private long _writeRetries;

    private readonly LatencySampler _readLatency = new();
    private readonly LatencySampler _writeLatency = new();

    public void ReadStarted() => Interlocked.Increment(ref _readOperations);
    public void ReadSucceeded() => Interlocked.Increment(ref _successfulReads);
    public void ReadValidated() => Interlocked.Increment(ref _validatedReads);
    public void ValidationFailed() => Interlocked.Increment(ref _validationFailures);
    public void StaleRead() => Interlocked.Increment(ref _staleReads);
    public void CacheAheadRead() => Interlocked.Increment(ref _cacheAheadReads);
    public void ReadFailed() => Interlocked.Increment(ref _readFailures);
    public void WriteStarted() => Interlocked.Increment(ref _writeOperations);
    public void WriteCommitted() => Interlocked.Increment(ref _committedWrites);
    public void WriteFailed() => Interlocked.Increment(ref _failedWrites);
    public void WriteConflict() => Interlocked.Increment(ref _writeConflicts);
    public void WriteRetry() => Interlocked.Increment(ref _writeRetries);
    public void RecordReadLatency(double ms) => _readLatency.Record(ms);
    public void RecordWriteLatency(double ms) => _writeLatency.Record(ms);

    public ExperimentResult BuildResult(
        string strategy,
        WorkloadOptions options,
        TimeSpan elapsed,
        long cacheHits,
        long cacheMisses,
        FaultInjectionSnapshot faults)
    {
        var readLatency = _readLatency.Summarize();
        var writeLatency = _writeLatency.Summarize();
        var reads = Interlocked.Read(ref _readOperations);
        var writes = Interlocked.Read(ref _writeOperations);
        var seconds = Math.Max(elapsed.TotalSeconds, 0.000001);

        return new ExperimentResult(
            strategy,
            options.Workers,
            options.ReadPercent,
            options.WritePercent,
            elapsed.TotalSeconds,
            reads,
            Interlocked.Read(ref _successfulReads),
            Interlocked.Read(ref _validatedReads),
            Interlocked.Read(ref _validationFailures),
            Interlocked.Read(ref _staleReads),
            Interlocked.Read(ref _cacheAheadReads),
            Interlocked.Read(ref _readFailures),
            writes,
            Interlocked.Read(ref _committedWrites),
            Interlocked.Read(ref _failedWrites),
            Interlocked.Read(ref _writeConflicts),
            Interlocked.Read(ref _writeRetries),
            cacheHits,
            cacheMisses,
            faults.CacheFailuresInjected,
            faults.InvalidationsFailed,
            faults.ArtificialDelaysApplied,
            faults.OutOfOrderDelaysApplied,
            (reads + writes) / seconds,
            readLatency,
            writeLatency);
    }
}

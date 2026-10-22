# CacheConsistency configurable workload runner

This is the starting point for repeatable workload experiments. The caching strategy classes and `ICacheStrategy` interface are not changed.

## Files

- `Program.cs` — connects to PostgreSQL/Redis, reads command-line configuration, runs experiments, and writes result files.
- `WorkloadOptions.cs` — workload and fault configuration plus CLI parsing.
- `WorkloadExperimentRunner.cs` — concurrent workers, read/write generation, optimistic-write retries, and stale-read checks.
- `FaultInjectingRedisDatabase.cs` — `DispatchProxy` around StackExchange.Redis `IDatabase` for delays, cache failures, failed `KeyDelete` invalidations, and extra random delays to provoke out-of-order cache mutations.
- `WorkloadMetrics.cs` — counters and bounded reservoir-sampled latency percentiles.
- `ExperimentResultWriter.cs` — JSON and CSV output.

The old `Demo.cs` can stay in the project, but `Program.cs` no longer invokes it.

## Examples

```bash
# Baseline: 10 workers, 90% reads / 10% writes, 20 seconds

docker compose run --rm app --workers 10 --ratio 90/10 --duration-seconds 20

# 50 workers, 80/20, artificial Redis delays and failures

docker compose run --rm app --workers 50 --ratio 80/20 --duration-seconds 30 \
  --min-delay-ms 1 --max-delay-ms 20 \
  --cache-failure-rate 0.01 \
  --invalidation-failure-rate 0.05 \
  --out-of-order-rate 0.10 --out-of-order-delay-ms 100

# 100 workers, 50/50

docker compose run --rm app --workers 100 --ratio 50/50 --duration-seconds 30
```

Rates are probabilities from `0.0` to `1.0` (`0.01` means 1%). Results are written to timestamped JSON and CSV files in `results/` by default.

## Metrics and interpretation

- **Stale reads**: a strategy read returns a product version below the database version read immediately afterward. The report includes the number of successfully validated reads and validation failures; concurrent writes between the cache read and validation query can conservatively count as stale.
- **Cache-ahead reads**: cache version is greater than the database version. This can indicate an incorrect/out-of-order cache value.
- **Latency**: strategy read/write call duration. The extra database query used to validate a read is excluded from measured read latency, but it still adds database load while verification is enabled.
- **Write failures**: includes exceptions during a write path. A cache exception can happen after PostgreSQL commits, so a failed operation in the report does not guarantee that the database write rolled back.
- **Out-of-order simulation**: selected `StringSet*`/cache mutation calls are delayed before Redis executes them. This raises the likelihood of completion-order inversions; it does not guarantee every run will contain an inversion.

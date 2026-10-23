using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CacheConsistency.Benchmarks;

public static class ExperimentResultWriter
{
    public static async Task<(string JsonPath, string CsvPath)> WriteAsync(
        IReadOnlyList<ExperimentResult> results,
        string outputDirectory,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        var jsonPath = Path.Combine(outputDirectory, $"cache-consistency-{stamp}.json");
        var csvPath = Path.Combine(outputDirectory, $"cache-consistency-{stamp}.csv");

        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(jsonPath, json, ct);

        var csv = new StringBuilder();
        csv.AppendLine("strategy,workers,read_percent,write_percent,elapsed_seconds,read_operations,successful_reads,validated_reads,validation_failures,stale_reads,stale_read_percent,cache_ahead_reads,read_failures,write_operations,committed_writes,failed_writes,write_conflicts,write_retries,cache_hits,cache_misses,cache_failures_injected,invalidations_failed,artificial_delays,out_of_order_delays,operations_per_second,read_mean_ms,read_p50_ms,read_p95_ms,read_p99_ms,write_mean_ms,write_p50_ms,write_p95_ms,write_p99_ms");

        foreach (var r in results)
        {
            var stalePercent = r.ValidatedReads == 0 ? 0 : 100.0 * r.StaleReads / r.ValidatedReads;
            object?[] fields =
            {
                r.Strategy, r.Workers, r.ReadPercent, r.WritePercent,
                r.ElapsedSeconds, r.ReadOperations, r.SuccessfulReads, r.ValidatedReads,
                r.ValidationFailures, r.StaleReads, stalePercent, r.CacheAheadReads,
                r.ReadFailures, r.WriteOperations,
                r.CommittedWrites, r.FailedWrites, r.WriteConflicts, r.WriteRetries,
                r.CacheHits, r.CacheMisses, r.CacheFailuresInjected, r.InvalidationsFailed,
                r.ArtificialDelaysApplied, r.OutOfOrderDelaysApplied, r.OperationsPerSecond,
                r.ReadLatencyMs.MeanMs, r.ReadLatencyMs.P50Ms, r.ReadLatencyMs.P95Ms, r.ReadLatencyMs.P99Ms,
                r.WriteLatencyMs.MeanMs, r.WriteLatencyMs.P50Ms, r.WriteLatencyMs.P95Ms, r.WriteLatencyMs.P99Ms
            };
            csv.AppendLine(string.Join(",", fields.Select(CsvValue)));
        }

        await File.WriteAllTextAsync(csvPath, csv.ToString(), ct);
        return (jsonPath, csvPath);
    }

    private static string CsvValue(object? value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n'))
            return $"\"{text.Replace("\"", "\"\"")}\"";
        return text;
    }
}

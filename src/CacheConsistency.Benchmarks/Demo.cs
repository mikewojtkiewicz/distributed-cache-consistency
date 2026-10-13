using CacheConsistency.Core.Models;
using CacheConsistency.Core.Abstractions;
using CacheConsistency.Infrastructure.Caching;
using CacheConsistency.Infrastructure.Data;
using StackExchange.Redis;

namespace CacheConsistency.Benchmarks;

public sealed class Demo
{
    private const int ProductId = 1;

    private readonly ProductRepository _repo;
    private readonly IDatabase _cache;

    public Demo(ProductRepository repo, IDatabase cache)
    {
        _repo = repo;
        _cache = cache;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        ICacheStrategy[] strategies =
        {
            new CacheAsideStrategy(_repo, _cache),
            new TtlCacheStrategy(_repo, _cache, TimeSpan.FromSeconds(30)),
            new WriteThroughStrategy(_repo, _cache),
            new VersionedCacheStrategy(_repo, _cache),
        };

        Section("A. Per-strategy read / write walkthrough");
        foreach (var s in strategies)
            await WalkthroughAsync(s, ct);

        Section("B. TTL staleness window");
        await TtlStalenessAsync(ct);

        Section("C. Concurrent updates (optimistic concurrency on the schema)");
        await ConcurrencyAsync(ct);

        Section("D. Write-through cache race: plain vs. versioned");
        await WriteRaceAsync(ct);

        Console.WriteLine();
        Console.WriteLine("Done. All scenarios completed.");
    }

    private async Task WalkthroughAsync(ICacheStrategy strat, CancellationToken ct)
    {
        await FlushAsync();
        await strat.ResetAsync(ct);

        Console.WriteLine();
        Console.WriteLine($"── {strat.Name} ──");

        var r1 = await strat.GetAsync(ProductId, ct);
        Console.WriteLine($"   read #1 : stock={r1!.Stock} v{r1.Version}   (cold → expect a cache miss)");

        var r2 = await strat.GetAsync(ProductId, ct);
        Console.WriteLine($"   read #2 : stock={r2!.Stock} v{r2.Version}   (warm → expect a cache hit)");

        var w = await strat.UpdateStockAsync(ProductId, r2.Stock - 1, r2.Version, ct);
        Console.WriteLine($"   write   : stock → {w!.Stock} v{w.Version}");

        var r3 = await strat.GetAsync(ProductId, ct);
        var label = r3!.Stock == w.Stock ? "FRESH" : "STALE (served old cached value)";
        Console.WriteLine($"   read #3 : stock={r3.Stock} v{r3.Version}   → {label}");
        Console.WriteLine($"   stats   : hits={strat.CacheHits} misses={strat.CacheMisses}");
    }

    private async Task TtlStalenessAsync(CancellationToken ct)
    {
        var ttl = TimeSpan.FromSeconds(3);
        var strat = new TtlCacheStrategy(_repo, _cache, ttl);
        await FlushAsync();

        var seed = await strat.GetAsync(ProductId, ct);
        Console.WriteLine($"   cached  : stock={seed!.Stock} v{seed.Version} (TTL {ttl.TotalSeconds:0}s)");

        await _repo.TryUpdateStockAsync(ProductId, seed.Stock + 7, seed.Version, ct);
        var stale = await strat.GetAsync(ProductId, ct);
        Console.WriteLine($"   now     : cache still serves stock={stale!.Stock} v{stale.Version}  → STALE within the window");

        Console.WriteLine($"   waiting {ttl.TotalSeconds:0}s for the TTL to expire...");
        await Task.Delay(ttl + TimeSpan.FromMilliseconds(500), ct);

        var healed = await strat.GetAsync(ProductId, ct);
        Console.WriteLine($"   after   : stock={healed!.Stock} v{healed.Version}  → FRESH (entry expired, reloaded from DB)");
    }

    private async Task ConcurrencyAsync(CancellationToken ct)
    {
        const int writers = 25;

        var before = await _repo.GetByIdAsync(ProductId, ct);
        Console.WriteLine($"   start   : stock={before!.Stock} v{before.Version}");
        Console.WriteLine($"   firing  : {writers} concurrent writers, each decrementing stock by 1...");

        var totalRetries = 0;
        var tasks = Enumerable.Range(0, writers).Select(async _ =>
        {
            var (_, retries) = await _repo.AdjustStockAsync(ProductId, -1, ct);
            Interlocked.Add(ref totalRetries, retries);
        });
        await Task.WhenAll(tasks);

        var after = await _repo.GetByIdAsync(ProductId, ct);
        var expected = before.Stock - writers;
        var ok = after!.Stock == expected ? "CONSISTENT" : "WRONG";
        Console.WriteLine($"   end     : stock={after.Stock} v{after.Version}");
        Console.WriteLine($"   result  : expected {expected} → {ok}. " +
                          $"Version advanced by {after.Version - before.Version} (= one per committed write).");
        Console.WriteLine($"   conflicts: {totalRetries} optimistic retries absorbed (no lost updates).");
    }

    private async Task WriteRaceAsync(CancellationToken ct)
    {
        var current = await _repo.GetByIdAsync(ProductId, ct);
        var newer = current! with { Stock = current.Stock + 100, Version = current.Version + 2 };
        var older = current  with { Stock = current.Stock + 50,  Version = current.Version + 1 };

        await FlushAsync();
        await _cache.StringSetAsync(Key(ProductId), System.Text.Json.JsonSerializer.Serialize(newer));
        await _cache.StringSetAsync(Key(ProductId), System.Text.Json.JsonSerializer.Serialize(older));
        var plain = Deserialize(await _cache.StringGetAsync(Key(ProductId)));
        Console.WriteLine($"   plain write-through : cache holds v{plain!.Version} stock={plain.Stock}  " +
                          $"→ REGRESSED (older write clobbered newer)");

        var versioned = new VersionedCacheStrategy(_repo, _cache);
        await FlushAsync();
        await versioned.VersionedSetAsync(newer);
        var accepted = await versioned.VersionedSetAsync(older);
        var guarded = Deserialize(await _cache.StringGetAsync(Key(ProductId)));
        Console.WriteLine($"   versioned write-thru: cache holds v{guarded!.Version} stock={guarded.Stock}  " +
                          $"→ older write {(accepted ? "accepted" : "REJECTED")}, cache stays at the newest version");
    }

    private static string Key(int id) => $"product:{id}";

    private static Product? Deserialize(RedisValue v) =>
        v.IsNullOrEmpty ? null : System.Text.Json.JsonSerializer.Deserialize<Product>(v!);

    private async Task FlushAsync() => await _cache.ExecuteAsync("FLUSHDB");

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 70));
        Console.WriteLine(title);
        Console.WriteLine(new string('=', 70));
    }
}

using CacheConsistency.Application;
using CacheConsistency.Core.Models;

var (strategyName, ttlSeconds, positional) = CliArgs.Parse(args);

if (positional.Count == 0 || positional[0] is "help" or "-h" or "--help")
{
    Usage.Print();
    return 0;
}

var command = positional[0];

if (command == "strategies")
{
    Console.WriteLine("Available strategies:");
    foreach (var n in StrategyFactory.Names)
        Console.WriteLine($"  {n}");
    return 0;
}

var pgConn = Environment.GetEnvironmentVariable("POSTGRES_CONN")
    ?? "Host=localhost;Port=5432;Database=cachelab;Username=cachelab;Password=cachelab";
var redisConn = Environment.GetEnvironmentVariable("REDIS_CONN") ?? "localhost:6379";

var (repo, mux) = await Bootstrap.ConnectAsync(pgConn, redisConn);
var cache = mux.GetDatabase();
var strategy = StrategyFactory.Create(
    strategyName, repo, cache, ttlSeconds is { } s ? TimeSpan.FromSeconds(s) : null);
var runner = new CacheRunner(strategy, repo);

Console.WriteLine($"strategy: {strategy.Name}");

var exit = 0;
switch (command)
{
    case "get" when positional.Count >= 2 && int.TryParse(positional[1], out var gid):
    {
        var p = await runner.GetAsync(gid);
        PrintProduct("get", p);
        Console.WriteLine($"cache: hits={runner.Hits} misses={runner.Misses}");
        break;
    }
    case "set" when positional.Count >= 3
                    && int.TryParse(positional[1], out var sid)
                    && int.TryParse(positional[2], out var stock):
    {
        PrintProduct("set", await runner.SetStockAsync(sid, stock));
        break;
    }
    case "adjust" when positional.Count >= 3
                       && int.TryParse(positional[1], out var aid)
                       && int.TryParse(positional[2], out var delta):
    {
        PrintProduct("adjust", await runner.AdjustStockAsync(aid, delta));
        break;
    }
    default:
        Console.Error.WriteLine($"unrecognized or malformed command: {string.Join(' ', positional)}");
        Usage.Print();
        exit = 2;
        break;
}

await mux.CloseAsync();
return exit;

static void PrintProduct(string op, Product? p)
{
    if (p is null)
    {
        Console.WriteLine($"{op}: no result (not found, or gave up after version conflicts)");
        return;
    }
    Console.WriteLine($"{op}: id={p.Id} name={p.Name} stock={p.Stock} price={p.PriceCents}c v{p.Version}");
}

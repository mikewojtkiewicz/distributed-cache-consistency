using CacheConsistency.Benchmarks;
using CacheConsistency.Core.Models;
using CacheConsistency.Infrastructure.Data;
using StackExchange.Redis;

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
{
    WorkloadOptions.PrintHelp();
    return 0;
}

var options = WorkloadOptions.Parse(args);
var pgConn = Environment.GetEnvironmentVariable("POSTGRES_CONN")
             ?? "Host=localhost;Port=5432;Database=cachelab;Username=cachelab;Password=cachelab";
var redisConn = Environment.GetEnvironmentVariable("REDIS_CONN") ?? "localhost:6379";

Console.WriteLine("CacheConsistency — configurable PostgreSQL + Redis workload");
Console.WriteLine($"  postgres : {Redact(pgConn)}");
Console.WriteLine($"  redis    : {redisConn}");
Console.WriteLine($"  workload : workers={options.Workers}, ratio={options.ReadPercent}/{options.WritePercent}, duration={options.Duration.TotalSeconds:0.##}s");
Console.WriteLine($"  faults   : Redis delay={options.MinRedisDelayMs}-{options.MaxRedisDelayMs}ms, " +
                  $"cache failure={options.CacheFailureRate:P1}, invalidation failure={options.InvalidationFailureRate:P1}, " +
                  $"out-of-order delay rate={options.OutOfOrderRate:P1}");

var repo = new ProductRepository(pgConn);
await WaitForPostgresAsync(repo, options.ProductId);
var mux = await ConnectRedisAsync(redisConn);
try
{
    var runner = new WorkloadExperimentRunner(repo, mux.GetDatabase());
    var results = await runner.RunAllAsync(options);
    var paths = await ExperimentResultWriter.WriteAsync(results, options.OutputDirectory);
    Console.WriteLine();
    Console.WriteLine($"JSON results: {Path.GetFullPath(paths.JsonPath)}");
    Console.WriteLine($"CSV results : {Path.GetFullPath(paths.CsvPath)}");
}
finally
{
    await mux.CloseAsync();
}

return 0;

static async Task WaitForPostgresAsync(ProductRepository repo, int productId)
{
    for (var attempt = 1; attempt <= 30; attempt++)
    {
        Product? product;
        try
        {
            product = await repo.GetByIdAsync(productId);
        }
        catch (Exception ex) when (attempt < 30)
        {
            Console.WriteLine($"  postgres : not ready (attempt {attempt}/30): {ex.Message.Trim()}");
            await Task.Delay(1000);
            continue;
        }

        if (product is null)
            throw new InvalidOperationException($"Product {productId} was not found in PostgreSQL.");

        Console.WriteLine("  postgres : connected.");
        return;
    }

    throw new TimeoutException($"PostgreSQL did not become available or product {productId} does not exist.");
}

static async Task<IConnectionMultiplexer> ConnectRedisAsync(string conn)
{
    var options = ConfigurationOptions.Parse(conn);
    options.AbortOnConnectFail = false;
    options.ConnectRetry = 5;
    options.ConnectTimeout = 5000;

    var mux = await ConnectionMultiplexer.ConnectAsync(options);
    Console.WriteLine("  redis    : connected.");
    return mux;
}

static string Redact(string conn) =>
    System.Text.RegularExpressions.Regex.Replace(conn, @"(?i)(password=)[^;]*", "$1***");

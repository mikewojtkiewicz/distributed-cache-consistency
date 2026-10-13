using CacheConsistency.Benchmarks;
using CacheConsistency.Infrastructure.Data;
using StackExchange.Redis;

var pgConn = Environment.GetEnvironmentVariable("POSTGRES_CONN")
    ?? "Host=localhost;Port=5432;Database=cachelab;Username=cachelab;Password=cachelab";
var redisConn = Environment.GetEnvironmentVariable("REDIS_CONN")
    ?? "localhost:6379";

Console.WriteLine("CacheConsistency — PostgreSQL + Redis caching strategies");
Console.WriteLine($"  postgres : {Redact(pgConn)}");
Console.WriteLine($"  redis    : {redisConn}");

var repo = new ProductRepository(pgConn);

await WaitForPostgresAsync(repo);
var mux = await ConnectRedisAsync(redisConn);
var cache = mux.GetDatabase();

await new Demo(repo, cache).RunAsync();

await mux.CloseAsync();
return 0;

static async Task WaitForPostgresAsync(ProductRepository repo)
{
    for (var attempt = 1; attempt <= 30; attempt++)
    {
        try
        {
            await repo.GetByIdAsync(1);
            Console.WriteLine("  postgres : connected.");
            return;
        }
        catch (Exception ex) when (attempt < 30)
        {
            Console.WriteLine($"  postgres : not ready (attempt {attempt}/30): {ex.Message.Trim()}");
            await Task.Delay(1000);
        }
    }
    throw new TimeoutException("PostgreSQL did not become available in time.");
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

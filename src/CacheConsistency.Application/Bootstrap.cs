using CacheConsistency.Core.Abstractions;
using CacheConsistency.Infrastructure.Data;
using StackExchange.Redis;

namespace CacheConsistency.Application;

public static class Bootstrap
{
    public static async Task<(ProductRepository repo, IConnectionMultiplexer mux)> ConnectAsync(
        string pgConn, string redisConn)
    {
        var repo = new ProductRepository(pgConn);
        await WaitForPostgresAsync(repo);
        var mux = await ConnectRedisAsync(redisConn);
        return (repo, mux);
    }

    private static async Task WaitForPostgresAsync(IProductRepository repo)
    {
        for (var attempt = 1; attempt <= 30; attempt++)
        {
            try
            {
                await repo.GetByIdAsync(1);
                return;
            }
            catch when (attempt < 30)
            {
                await Task.Delay(1000);
            }
        }
        throw new TimeoutException("PostgreSQL did not become available in time.");
    }

    private static async Task<IConnectionMultiplexer> ConnectRedisAsync(string conn)
    {
        var options = ConfigurationOptions.Parse(conn);
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 5;
        options.ConnectTimeout = 5000;
        return await ConnectionMultiplexer.ConnectAsync(options);
    }
}

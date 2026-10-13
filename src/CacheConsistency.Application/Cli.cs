namespace CacheConsistency.Application;

public static class CliArgs
{
    public static (string strategy, int? ttlSeconds, List<string> positional) Parse(string[] args)
    {
        var strategy = "cache-aside";
        int? ttl = null;
        var positional = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--strategy" or "-s" when i + 1 < args.Length:
                    strategy = args[++i];
                    break;
                case "--ttl" when i + 1 < args.Length && int.TryParse(args[i + 1], out var t):
                    ttl = t;
                    i++;
                    break;
                default:
                    positional.Add(args[i]);
                    break;
            }
        }

        return (strategy, ttl, positional);
    }
}

public static class Usage
{
    public static void Print()
    {
        Console.WriteLine(
@"CacheConsistency runner CLI

Usage:
  run [--strategy <name>] [--ttl <seconds>] <command> [args]

Options:
  -s, --strategy <name>   cache-aside (default) | ttl | write-through | versioned
      --ttl <seconds>     TTL for the 'ttl' strategy (default 30)

Commands:
  get <id>                Read a product through the cache
  set <id> <stock>        Set a product's stock (optimistic, retries on conflict)
  adjust <id> <delta>     Read-modify-write a product's stock by <delta>
  strategies              List available caching strategies
  help                    Show this help

Environment:
  POSTGRES_CONN   default Host=localhost;Port=5432;Database=cachelab;Username=cachelab;Password=cachelab
  REDIS_CONN      default localhost:6379

Examples:
  run get 1
  run --strategy versioned set 1 95
  run -s ttl --ttl 10 get 1
  run adjust 1 -5");
    }
}

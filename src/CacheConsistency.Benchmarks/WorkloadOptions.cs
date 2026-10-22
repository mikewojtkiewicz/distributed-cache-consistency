using System.Globalization;

namespace CacheConsistency.Benchmarks;

public sealed class WorkloadOptions
{
    public int Workers { get; set; } = 10;
    public int ReadPercent { get; set; } = 80;
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(10);
    public int ProductId { get; set; } = 1;
    public int MaxWriteRetries { get; set; } = 10;
    public int TtlSeconds { get; set; } = 30;
    public bool VerifyReadsAgainstDatabase { get; set; } = true;

    public int MinRedisDelayMs { get; set; }
    public int MaxRedisDelayMs { get; set; }
    public double CacheFailureRate { get; set; }
    public double InvalidationFailureRate { get; set; }
    public double OutOfOrderRate { get; set; }
    public int OutOfOrderDelayMs { get; set; } = 100;

    public string OutputDirectory { get; set; } = "results";

    public int WritePercent => 100 - ReadPercent;

    public void Validate()
    {
        if (Workers < 1)
            throw new ArgumentOutOfRangeException(nameof(Workers), "Workers must be at least 1.");
        if (ReadPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(ReadPercent), "Read percentage must be between 0 and 100.");
        if (Duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(Duration), "Duration must be positive.");
        if (ProductId < 1)
            throw new ArgumentOutOfRangeException(nameof(ProductId), "Product ID must be positive.");
        if (MaxWriteRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(MaxWriteRetries), "Max write retries cannot be negative.");
        if (TtlSeconds < 1)
            throw new ArgumentOutOfRangeException(nameof(TtlSeconds), "TTL must be at least one second.");
        if (MinRedisDelayMs < 0 || MaxRedisDelayMs < MinRedisDelayMs)
            throw new ArgumentException("Redis delay bounds must satisfy 0 <= min-delay-ms <= max-delay-ms.");
        if (OutOfOrderDelayMs < 0)
            throw new ArgumentOutOfRangeException(nameof(OutOfOrderDelayMs), "Out-of-order delay cannot be negative.");
        ValidateRate(nameof(CacheFailureRate), CacheFailureRate);
        ValidateRate(nameof(InvalidationFailureRate), InvalidationFailureRate);
        ValidateRate(nameof(OutOfOrderRate), OutOfOrderRate);
        if (string.IsNullOrWhiteSpace(OutputDirectory))
            throw new ArgumentException("Output directory cannot be empty.", nameof(OutputDirectory));
    }

    public static WorkloadOptions Parse(string[] args)
    {
        var options = new WorkloadOptions();
        for (var i = 0; i < args.Length; i++)
        {
            string Value()
            {
                if (i + 1 >= args.Length)
                    throw new ArgumentException($"Missing value for {args[i]}.");
                return args[++i];
            }

            switch (args[i])
            {
                case "--workers":
                    options.Workers = int.Parse(Value(), CultureInfo.InvariantCulture);
                    break;
                case "--ratio":
                {
                    var parts = Value().Split('/');
                    if (parts.Length != 2)
                        throw new ArgumentException("--ratio must look like 90/10, 80/20, or 50/50.");
                    var reads = int.Parse(parts[0], CultureInfo.InvariantCulture);
                    var writes = int.Parse(parts[1], CultureInfo.InvariantCulture);
                    if (reads + writes != 100)
                        throw new ArgumentException("Read and write percentages must add up to 100.");
                    options.ReadPercent = reads;
                    break;
                }
                case "--read-percent":
                    options.ReadPercent = int.Parse(Value(), CultureInfo.InvariantCulture);
                    break;
                case "--duration-seconds":
                    options.Duration = TimeSpan.FromSeconds(double.Parse(Value(), CultureInfo.InvariantCulture));
                    break;
                case "--product-id":
                    options.ProductId = int.Parse(Value(), CultureInfo.InvariantCulture);
                    break;
                case "--max-write-retries":
                    options.MaxWriteRetries = int.Parse(Value(), CultureInfo.InvariantCulture);
                    break;
                case "--ttl-seconds":
                    options.TtlSeconds = int.Parse(Value(), CultureInfo.InvariantCulture);
                    break;
                case "--min-delay-ms":
                    options.MinRedisDelayMs = int.Parse(Value(), CultureInfo.InvariantCulture);
                    break;
                case "--max-delay-ms":
                    options.MaxRedisDelayMs = int.Parse(Value(), CultureInfo.InvariantCulture);
                    break;
                case "--cache-failure-rate":
                    options.CacheFailureRate = double.Parse(Value(), CultureInfo.InvariantCulture);
                    break;
                case "--invalidation-failure-rate":
                    options.InvalidationFailureRate = double.Parse(Value(), CultureInfo.InvariantCulture);
                    break;
                case "--out-of-order-rate":
                    options.OutOfOrderRate = double.Parse(Value(), CultureInfo.InvariantCulture);
                    break;
                case "--out-of-order-delay-ms":
                    options.OutOfOrderDelayMs = int.Parse(Value(), CultureInfo.InvariantCulture);
                    break;
                case "--output-dir":
                    options.OutputDirectory = Value();
                    break;
                case "--no-verify-reads":
                    options.VerifyReadsAgainstDatabase = false;
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]} (use --help for usage).");
            }
        }

        options.Validate();
        return options;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("CacheConsistency workload runner");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --workers N                    Concurrent workers (e.g. 10, 50, 100; default 10)");
        Console.WriteLine("  --ratio R/W                    Read/write ratio (90/10, 80/20, 50/50; default 80/20)");
        Console.WriteLine("  --duration-seconds N           Experiment duration (default 10)");
        Console.WriteLine("  --product-id N                 Product row used by the workload (default 1)");
        Console.WriteLine("  --max-write-retries N          Optimistic-concurrency retries per write (default 10)");
        Console.WriteLine("  --ttl-seconds N                TTL strategy lifetime (default 30)");
        Console.WriteLine("  --min-delay-ms N               Minimum delay before intercepted Redis operations");
        Console.WriteLine("  --max-delay-ms N               Maximum delay before intercepted Redis operations");
        Console.WriteLine("  --cache-failure-rate P         Probability from 0.0 to 1.0 of a cache-operation failure");
        Console.WriteLine("  --invalidation-failure-rate P  Probability from 0.0 to 1.0 of KeyDelete failure");
        Console.WriteLine("  --out-of-order-rate P          Probability from 0.0 to 1.0 of an extra write delay");
        Console.WriteLine("  --out-of-order-delay-ms N      Extra delay for selected writes (default 100)");
        Console.WriteLine("  --no-verify-reads              Disable source-of-truth read checks (faster, no stale-read count)");
        Console.WriteLine("  --output-dir PATH              Directory for JSON and CSV results (default results)");
        Console.WriteLine("  --help                         Show this help");
        Console.WriteLine();
        Console.WriteLine("Failure probabilities are decimals: 0.01 means 1%.");
    }

    private static void ValidateRate(string name, double rate)
    {
        if (double.IsNaN(rate) || rate is < 0 or > 1)
            throw new ArgumentOutOfRangeException(name, "Probability must be between 0.0 and 1.0.");
    }
}

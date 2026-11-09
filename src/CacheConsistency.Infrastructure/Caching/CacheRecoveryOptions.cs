namespace CacheConsistency.Infrastructure.Caching;

public sealed record CacheRecoveryOptions(bool Enabled, TimeSpan? SafetyTtl = null)
{
    public static readonly CacheRecoveryOptions Disabled = new(false);
}

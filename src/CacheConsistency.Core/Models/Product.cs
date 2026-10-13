namespace CacheConsistency.Core.Models;

public sealed record Product
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int Stock { get; init; }
    public int PriceCents { get; init; }
    public long Version { get; init; }
}

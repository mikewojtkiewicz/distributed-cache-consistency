using CacheConsistency.Benchmarks;
using Xunit;

namespace CacheConsistency.Tests;

public class LatencySamplerTests
{
    [Fact]
    public void Summarize_computes_percentiles_on_a_known_distribution()
    {
        var sampler = new LatencySampler();
        for (var i = 1; i <= 100; i++)
            sampler.Record(i);

        var p = sampler.Summarize();

        Assert.Equal(100, p.Samples);
        Assert.Equal(50.5, p.MeanMs, 3);
        Assert.Equal(50, p.P50Ms);
        Assert.Equal(95, p.P95Ms);
        Assert.Equal(99, p.P99Ms);
    }

    [Fact]
    public void Summarize_of_an_empty_sampler_is_all_zero()
    {
        var p = new LatencySampler().Summarize();
        Assert.Equal(0, p.Samples);
        Assert.Equal(0, p.P50Ms);
        Assert.Equal(0, p.P99Ms);
    }
}

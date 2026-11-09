using CacheConsistency.Benchmarks;
using Xunit;

namespace CacheConsistency.Tests;

public class WorkloadOptionsTests
{
    [Fact]
    public void Parse_uses_documented_defaults()
    {
        var o = WorkloadOptions.Parse(Array.Empty<string>());
        Assert.Equal(10, o.Workers);
        Assert.Equal(80, o.ReadPercent);
        Assert.Equal(20, o.WritePercent);
        Assert.Equal(1, o.KeyCount);
        Assert.False(o.Recovery);
    }

    [Fact]
    public void Parse_reads_ratio_and_derives_write_percent()
    {
        var o = WorkloadOptions.Parse(new[] { "--ratio", "90/10" });
        Assert.Equal(90, o.ReadPercent);
        Assert.Equal(10, o.WritePercent);
    }

    [Fact]
    public void Parse_rejects_a_ratio_that_does_not_sum_to_100()
    {
        Assert.Throws<ArgumentException>(() => WorkloadOptions.Parse(new[] { "--ratio", "90/20" }));
    }

    [Fact]
    public void Parse_rejects_an_unknown_flag()
    {
        Assert.Throws<ArgumentException>(() => WorkloadOptions.Parse(new[] { "--not-a-flag" }));
    }

    [Fact]
    public void Parse_reads_recovery_and_multi_key_flags()
    {
        var o = WorkloadOptions.Parse(new[]
        {
            "--recovery", "--safety-ttl-seconds", "15",
            "--key-count", "20", "--hot-keys", "3", "--hot-share", "0.9"
        });
        Assert.True(o.Recovery);
        Assert.Equal(15, o.SafetyTtlSeconds);
        Assert.Equal(20, o.KeyCount);
        Assert.Equal(3, o.HotKeys);
        Assert.Equal(0.9, o.HotShare);
    }

    [Fact]
    public void Parse_rejects_hot_keys_greater_than_key_count()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorkloadOptions.Parse(new[] { "--key-count", "5", "--hot-keys", "9" }));
    }

    [Fact]
    public void NextKey_stays_within_the_configured_range()
    {
        var o = WorkloadOptions.Parse(new[] { "--product-id", "1", "--key-count", "5" });
        for (var i = 0; i < 1000; i++)
        {
            var key = o.NextKey();
            Assert.InRange(key, 1, 5);
        }
    }
}

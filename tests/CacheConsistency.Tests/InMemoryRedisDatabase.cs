using System.Reflection;
using System.Text.Json;
using StackExchange.Redis;

namespace CacheConsistency.Tests;

public class InMemoryRedisDatabase : DispatchProxy
{
    private readonly Dictionary<string, RedisValue> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _failures = new(StringComparer.Ordinal);

    public static (IDatabase Database, InMemoryRedisDatabase Controller) Create()
    {
        var database = DispatchProxy.Create<IDatabase, InMemoryRedisDatabase>();
        return (database, (InMemoryRedisDatabase)(object)database);
    }

    public void FailNext(string methodName) => _failures[methodName] = _failures.GetValueOrDefault(methodName) + 1;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod?.Name ?? throw new InvalidOperationException("Missing Redis method.");
        args ??= Array.Empty<object?>();
        if (_failures.TryGetValue(method, out var remaining) && remaining > 0)
        {
            if (remaining == 1)
                _failures.Remove(method);
            else
                _failures[method] = remaining - 1;
            throw new RedisException($"Injected test failure in {method}.");
        }

        if (method == "ScriptEvaluateAsync")
            return VersionedSetAsync(args);

        var key = args.Length > 0 ? args[0]?.ToString() ?? string.Empty : string.Empty;
        return method switch
        {
            "StringGetAsync" => Task.FromResult(_values.GetValueOrDefault(key)),
            "StringSetAsync" => SetAsync(key, (RedisValue)args[1]!),
            "KeyDeleteAsync" => Task.FromResult(_values.Remove(key)),
            _ => throw new NotSupportedException($"The test Redis database does not implement {method}.")
        };
    }

    private Task<bool> SetAsync(string key, RedisValue value)
    {
        _values[key] = value;
        return Task.FromResult(true);
    }

    // Emulates the version-guarded compare-and-set Lua script used by VersionedCacheStrategy:
    // overwrite only when the incoming version is strictly greater than the stored version.
    private Task<RedisResult> VersionedSetAsync(object?[] args)
    {
        var keys = (RedisKey[])args[1]!;
        var values = (RedisValue[])args[2]!;
        var key = keys[0].ToString();
        var incoming = values[0];
        var incomingVersion = (long)values[1];

        if (_values.TryGetValue(key, out var existing) && !existing.IsNullOrEmpty)
        {
            var storedVersion = JsonSerializer.Deserialize<VersionEnvelope>(existing!)?.Version ?? long.MinValue;
            if (storedVersion >= incomingVersion)
                return Task.FromResult(RedisResult.Create((RedisValue)0));
        }

        _values[key] = incoming;
        return Task.FromResult(RedisResult.Create((RedisValue)1));
    }

    private sealed record VersionEnvelope(long Version);
}

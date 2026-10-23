using System.Reflection;
using System.Runtime.ExceptionServices;
using StackExchange.Redis;

namespace CacheConsistency.Benchmarks;

public sealed record FaultInjectionSnapshot(
    long CacheFailuresInjected,
    long InvalidationsFailed,
    long ArtificialDelaysApplied,
    long OutOfOrderDelaysApplied);

/// <summary>
/// Intercepts Redis database calls without changing the cache strategies. Delays are applied
/// before the Redis command, which widens the interval between a database write and its cache
/// update and can let concurrent cache commands complete out of order.
/// </summary>
public class FaultInjectingRedisDatabase : DispatchProxy
{
    private IDatabase? _inner;
    private WorkloadOptions? _options;
    private int _enabled;
    private long _cacheFailuresInjected;
    private long _invalidationsFailed;
    private long _artificialDelaysApplied;
    private long _outOfOrderDelaysApplied;

    public static (IDatabase Database, FaultInjectingRedisDatabase Controller) Create(
        IDatabase inner,
        WorkloadOptions options)
    {
        var database = DispatchProxy.Create<IDatabase, FaultInjectingRedisDatabase>();
        var controller = (FaultInjectingRedisDatabase)(object)database;
        controller._inner = inner;
        controller._options = options;
        controller._enabled = 0;
        return (database, controller);
    }

    public void Enable() => Volatile.Write(ref _enabled, 1);
    public void Disable() => Volatile.Write(ref _enabled, 0);

    public FaultInjectionSnapshot Snapshot() => new(
        Interlocked.Read(ref _cacheFailuresInjected),
        Interlocked.Read(ref _invalidationsFailed),
        Interlocked.Read(ref _artificialDelaysApplied),
        Interlocked.Read(ref _outOfOrderDelaysApplied));

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null || _inner is null)
            throw new InvalidOperationException("The Redis fault proxy has not been initialized.");

        var returnType = targetMethod.ReturnType;
        if (returnType == typeof(Task))
            return InvokeTaskAsync(targetMethod, args);

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            var helper = typeof(FaultInjectingRedisDatabase).GetMethod(nameof(InvokeTaskOfTAsync), BindingFlags.Instance | BindingFlags.NonPublic)!;
            return helper.MakeGenericMethod(resultType).Invoke(this, new object?[] { targetMethod, args });
        }

        BeforeCommandAsync(targetMethod.Name).GetAwaiter().GetResult();
        try
        {
            return targetMethod.Invoke(_inner, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private async Task InvokeTaskAsync(MethodInfo method, object?[]? args)
    {
        await BeforeCommandAsync(method.Name).ConfigureAwait(false);
        var task = (Task)InvokeInner(method, args)!;
        await task.ConfigureAwait(false);
    }

    private async Task<T> InvokeTaskOfTAsync<T>(MethodInfo method, object?[]? args)
    {
        await BeforeCommandAsync(method.Name).ConfigureAwait(false);
        var task = (Task<T>)InvokeInner(method, args)!;
        return await task.ConfigureAwait(false);
    }

    private object? InvokeInner(MethodInfo method, object?[]? args)
    {
        try
        {
            return method.Invoke(_inner, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private async Task BeforeCommandAsync(string methodName)
    {
        if (Volatile.Read(ref _enabled) == 0 || !IsCacheDataCommand(methodName))
            return;

        var options = _options!;
        var delay = options.MinRedisDelayMs == options.MaxRedisDelayMs
            ? options.MinRedisDelayMs
            : Random.Shared.Next(options.MinRedisDelayMs, options.MaxRedisDelayMs + 1);

        if (delay > 0)
        {
            Interlocked.Increment(ref _artificialDelaysApplied);
            await Task.Delay(delay).ConfigureAwait(false);
        }

        if (IsCacheMutation(methodName) && options.OutOfOrderDelayMs > 0 &&
            Random.Shared.NextDouble() < options.OutOfOrderRate)
        {
            Interlocked.Increment(ref _outOfOrderDelaysApplied);
            Interlocked.Increment(ref _artificialDelaysApplied);
            await Task.Delay(options.OutOfOrderDelayMs).ConfigureAwait(false);
        }

        if (IsInvalidation(methodName) && Random.Shared.NextDouble() < options.InvalidationFailureRate)
        {
            Interlocked.Increment(ref _invalidationsFailed);
            throw new RedisException($"Injected invalidation failure in {methodName}.");
        }

        if (Random.Shared.NextDouble() < options.CacheFailureRate)
        {
            Interlocked.Increment(ref _cacheFailuresInjected);
            throw new RedisException($"Injected cache failure in {methodName}.");
        }
    }

    private static bool IsCacheDataCommand(string methodName) =>
        methodName.StartsWith("StringGet", StringComparison.Ordinal) ||
        methodName.StartsWith("StringSet", StringComparison.Ordinal) ||
        methodName.StartsWith("KeyDelete", StringComparison.Ordinal) ||
        methodName.StartsWith("KeyExists", StringComparison.Ordinal) ||
        methodName.StartsWith("KeyExpire", StringComparison.Ordinal) ||
        methodName.StartsWith("Hash", StringComparison.Ordinal) ||
        methodName.StartsWith("Set", StringComparison.Ordinal) ||
        methodName.StartsWith("List", StringComparison.Ordinal) ||
        methodName.StartsWith("SortedSet", StringComparison.Ordinal) ||
        methodName.StartsWith("Stream", StringComparison.Ordinal) ||
        methodName.StartsWith("ScriptEvaluate", StringComparison.Ordinal);

    private static bool IsInvalidation(string methodName) =>
        methodName.StartsWith("KeyDelete", StringComparison.Ordinal);

    private static bool IsCacheMutation(string methodName) =>
        methodName.StartsWith("StringSet", StringComparison.Ordinal) ||
        methodName.StartsWith("KeyDelete", StringComparison.Ordinal) ||
        methodName.StartsWith("HashSet", StringComparison.Ordinal) ||
        methodName.StartsWith("SetAdd", StringComparison.Ordinal) ||
        methodName.StartsWith("ScriptEvaluate", StringComparison.Ordinal);
}

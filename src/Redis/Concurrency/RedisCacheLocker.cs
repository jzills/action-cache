using ActionCache.Common.Concurrency;
using ActionCache.Redis.Concurrency.Locks;
using StackExchange.Redis;

namespace ActionCache.Redis.Concurrency;

/// <summary>
/// A distributed cache locker backed by Redis using the SET NX PX (SETNX) pattern.
/// Acquisition and release are each a single atomic Lua script, so no race conditions
/// can occur between check-and-set steps.
/// </summary>
public class RedisCacheLocker : CacheLockerBase<RedisCacheLock>
{
    private readonly IDatabase _database;

    // SET key token NX PX ttlMs — returns "OK" on success, null on failure.
    private const string AcquireScript =
        "return redis.call('SET', KEYS[1], ARGV[1], 'NX', 'PX', ARGV[2])";

    // DEL key only when the stored value matches our token (fencing).
    private const string ReleaseScript =
        "if redis.call('GET', KEYS[1]) == ARGV[1] then " +
        "  return redis.call('DEL', KEYS[1]) " +
        "else " +
        "  return 0 " +
        "end";

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCacheLocker"/> class.
    /// </summary>
    /// <param name="database">The Redis database used for lock operations.</param>
    /// <param name="lockDuration">TTL applied to the lock key in Redis.</param>
    /// <param name="lockTimeout">Maximum time <see cref="WaitForLockAsync"/> will poll.</param>
    public RedisCacheLocker(IDatabase database, TimeSpan lockDuration, TimeSpan lockTimeout)
        : base(lockDuration, lockTimeout)
    {
        _database = database;
    }

    /// <inheritdoc/>
    public override async Task<RedisCacheLock> TryAcquireLockAsync(string resource)
    {
        var cacheLock = new RedisCacheLock(resource, LockDuration, LockTimeout);

        var result = await _database.ScriptEvaluateAsync(
            AcquireScript,
            [(RedisKey)cacheLock.Key],
            [cacheLock.Token, (long)LockDuration.TotalMilliseconds]);

        cacheLock.IsAcquired = result.ToString() == "OK";
        return cacheLock;
    }

    /// <inheritdoc/>
    public override async Task ReleaseLockAsync(RedisCacheLock cacheLock)
    {
        if (!cacheLock.IsAcquired)
            return;

        await _database.ScriptEvaluateAsync(
            ReleaseScript,
            [(RedisKey)cacheLock.Key],
            [cacheLock.Token]);
    }

    /// <inheritdoc/>
    public override async Task<RedisCacheLock> WaitForLockAsync(string resource)
    {
        var deadline = DateTime.UtcNow.Add(LockTimeout);
        while (DateTime.UtcNow < deadline)
        {
            var cacheLock = await TryAcquireLockAsync(resource);
            if (cacheLock.IsAcquired)
                return cacheLock;

            await Task.Delay(100);
        }

        return new RedisCacheLock(resource, LockDuration, LockTimeout);
    }
}

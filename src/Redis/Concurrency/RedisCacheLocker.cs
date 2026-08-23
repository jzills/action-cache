using ActionCache.Common.Concurrency;
using ActionCache.Redis.Concurrency.Locks;
using StackExchange.Redis;

namespace ActionCache.Redis.Concurrency;

/// <summary>
/// A distributed cache locker backed by Redis using the SET NX PX (SETNX) pattern.
/// Acquisition and release are each a single atomic Lua script, so no race conditions
/// can occur between check-and-set steps.
/// </summary>
/// <remarks>
/// The only locker that enforces the lease: it becomes the lock key's TTL, so an operation
/// running longer than the lease loses its lock while still in flight and another caller may
/// acquire it. Release is token-fenced, so the original holder cannot then delete a lock it
/// no longer owns. Size the lease above the slowest operation it guards.
/// </remarks>
public class RedisCacheLocker : CacheLockerBase<RedisCacheLock>
{
    private readonly IDatabase _database;
    private readonly TimeSpan _leaseDuration;

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
    /// <param name="leaseDuration">
    /// TTL applied to the lock key. Redis is the only backend that can impose a lease, so it
    /// is a parameter here rather than shared state on the base locker.
    /// </param>
    /// <param name="lockTimeout">Maximum time <see cref="WaitForLockAsync"/> will poll.</param>
    public RedisCacheLocker(IDatabase database, TimeSpan leaseDuration, TimeSpan lockTimeout)
        : base(lockTimeout)
    {
        _database = database;
        _leaseDuration = leaseDuration;
    }

    /// <inheritdoc/>
    public override async Task<RedisCacheLock> TryAcquireLockAsync(string resource)
    {
        var cacheLock = new RedisCacheLock(resource, LockTimeout);

        var result = await _database.ScriptEvaluateAsync(
            AcquireScript,
            [(RedisKey)cacheLock.Key],
            [cacheLock.Token, (long)_leaseDuration.TotalMilliseconds]);

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

        return new RedisCacheLock(resource, LockTimeout);
    }
}

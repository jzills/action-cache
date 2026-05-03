using ActionCache.Common.Concurrency;

namespace ActionCache.Redis.Concurrency.Locks;

/// <summary>
/// Represents a Redis-backed distributed lock acquired via SET NX PX.
/// Holds the lock key and a unique fencing token used by the release script
/// to ensure only the owner can release the lock.
/// </summary>
public class RedisCacheLock : CacheLock
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCacheLock"/> class.
    /// </summary>
    /// <param name="resource">The logical resource being locked (used to build <see cref="Key"/>).</param>
    /// <param name="lockDuration">How long the lock is held in Redis (TTL on the key).</param>
    /// <param name="lockTimeout">Maximum time the locker will poll before giving up.</param>
    public RedisCacheLock(string resource, TimeSpan lockDuration, TimeSpan lockTimeout) : base(resource)
    {
        Duration = lockDuration;
        Timeout = lockTimeout;
        Key = $"Lock:{resource}";
        Token = Guid.NewGuid().ToString("N");
    }

    /// <summary>Redis key under which the lock is stored.</summary>
    public string Key { get; }

    /// <summary>Unique fencing token stored as the Redis value; used by the release script.</summary>
    public string Token { get; }
}

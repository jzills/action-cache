namespace ActionCache.Common.Concurrency;

/// <summary>
/// Controls how concurrent misses for one cache key are coalesced.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="ActionCacheEntryOptions"/>'s lock settings. Those
/// guard short read-modify-write operations on a namespace's key index and are measured in
/// milliseconds; a single-flight lock is held across the origin action for as long as that
/// action takes. Sizing one from the other gets at least one of them wrong.
/// </remarks>
public class ActionCacheSingleFlightOptions
{
    /// <summary>
    /// How long the leader may hold a key's lock before other callers may assume it died.
    /// </summary>
    /// <value>
    /// The default is 30 seconds. This must comfortably exceed the slowest action the cache
    /// fronts: if the lease expires while the leader is still running, a waiting caller
    /// acquires the lock and executes the action too, which is the stampede this exists to
    /// prevent.
    /// </value>
    /// <remarks>
    /// Only backends whose locks carry a time-to-live enforce this — Redis, where it is the
    /// lock key's TTL. The in-process semaphore and SQL Server's session-scoped
    /// <c>sp_getapplock</c> hold until released, so a lease cannot expire under them and a
    /// process that dies releases its lock by exiting.
    /// </remarks>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a caller waits for the leader before giving up and executing uncoalesced.
    /// </summary>
    /// <value>The default is 10 seconds.</value>
    /// <remarks>
    /// Waiting longer than the action takes is the point; a timeout here is a fallback, not
    /// the expected path. Timing out is never an error — the caller simply runs the action
    /// itself, consistent with the library's fail-open stance.
    /// </remarks>
    public TimeSpan WaitTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Throws when the configured values cannot coalesce anything.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when either value is not positive, or when the lease is not longer than the
    /// wait: a caller that waits the full timeout would then be guaranteed to find the
    /// leader's lease already expired, so every slow request would stampede.
    /// </exception>
    internal void Validate()
    {
        if (LeaseDuration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"{nameof(ActionCacheSingleFlightOptions)}.{nameof(LeaseDuration)} must be greater than zero.");
        }

        if (WaitTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"{nameof(ActionCacheSingleFlightOptions)}.{nameof(WaitTimeout)} must be greater than zero.");
        }

        if (LeaseDuration <= WaitTimeout)
        {
            throw new InvalidOperationException(
                $"{nameof(ActionCacheSingleFlightOptions)}.{nameof(LeaseDuration)} ({LeaseDuration}) must be longer " +
                $"than {nameof(WaitTimeout)} ({WaitTimeout}). A caller that waits the full timeout would otherwise " +
                "always find the leader's lease expired and execute the action anyway, defeating single-flight.");
        }
    }
}

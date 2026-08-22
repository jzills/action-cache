namespace ActionCache.Common.Concurrency;

/// <summary>
/// Represents a mechanism for acquiring and releasing distributed locks in a caching system.
/// </summary>
/// <typeparam name="T">The type of lock managed by the locker, which must inherit from <see cref="CacheLock"/>.</typeparam>
public interface ICacheLocker<T> : ICacheLockerHandler where T : CacheLock
{
    /// <summary>
    /// Attempts to acquire a lock on a specified resource asynchronously.
    /// </summary>
    /// <param name="resource">The unique identifier of the resource to lock.</param>
    /// <returns>
    /// A task representing the asynchronous operation. 
    /// The result is an instance of <typeparamref name="T"/> representing details about the lock.
    /// </returns>
    Task<T> TryAcquireLockAsync(string resource);

    /// <summary>
    /// Releases a previously acquired lock asynchronously.
    /// </summary>
    /// <param name="cacheLock">The lock to release.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ReleaseLockAsync(T cacheLock);

    /// <summary>
    /// Waits for a lock on a specified resource to be acquired asynchronously.
    /// </summary>
    /// <param name="resource">The unique identifier of the resource to lock.</param>
    /// <returns>
    /// A task representing the asynchronous operation. 
    /// The result is an instance of <typeparamref name="T"/> representing the acquired lock.
    /// </returns>
    Task<T> WaitForLockAsync(string resource);
}
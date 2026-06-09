using ActionCache.Common.Concurrency.Locks;

namespace ActionCache.Common.Concurrency;

/// <summary>
/// A no-operation <see cref="CacheLockerBase{TLock}"/> that immediately grants every lock request without any blocking or synchronization.
/// </summary>
public class NullCacheLocker : CacheLockerBase<NullCacheLock>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NullCacheLocker"/> class.
    /// </summary>
    public NullCacheLocker() : base(TimeSpan.Zero, TimeSpan.Zero)
    {
    }

    /// <summary>
    /// Releases the specified lock asynchronously as a no-operation and returns a completed task immediately.
    /// </summary>
    /// <param name="cacheLock">The lock to be released.</param>
    /// <returns>A completed task.</returns>
    public override Task ReleaseLockAsync(NullCacheLock cacheLock) => Task.CompletedTask;

    /// <summary>
    /// Returns a pre-acquired <see cref="NullCacheLock"/> for the specified resource without any blocking.
    /// </summary>
    /// <param name="resource">The resource to lock.</param>
    /// <returns>A completed task containing the acquired lock.</returns>
    public override Task<NullCacheLock> TryAcquireLockAsync(string resource) => 
        Task.FromResult(new NullCacheLock(resource));

    /// <summary>
    /// Returns a pre-acquired <see cref="NullCacheLock"/> for the specified resource without any waiting.
    /// </summary>
    /// <param name="resource">The resource to lock.</param>
    /// <returns>A completed task containing the acquired lock.</returns>
    public override Task<NullCacheLock> WaitForLockAsync(string resource) => TryAcquireLockAsync(resource);
}
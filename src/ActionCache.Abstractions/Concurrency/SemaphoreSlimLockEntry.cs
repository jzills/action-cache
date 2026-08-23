namespace ActionCache.Common.Concurrency;

/// <summary>
/// A ref-counted <see cref="SemaphoreSlim"/> held by <see cref="SemaphoreSlimCacheLocker"/> for a single resource.
/// </summary>
internal sealed class SemaphoreSlimLockEntry
{
    /// <summary>
    /// The semaphore guarding the resource. Binary: one permit.
    /// </summary>
    internal readonly SemaphoreSlim Semaphore = new(initialCount: 1, maxCount: 1);

    /// <summary>
    /// The number of holders and waiters currently referencing this entry.
    /// The entry is removed from the locker's map when this reaches zero.
    /// </summary>
    internal int RefCount;
}

namespace ActionCache.Common.Concurrency.Locks;

/// <summary>
/// Represents an in-process lock backed by a <see cref="SemaphoreSlim"/> held for a single resource.
/// </summary>
public class SemaphoreSlimLock : CacheLock
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SemaphoreSlimLock"/> class.
    /// </summary>
    /// <param name="resource">The resource this lock guards.</param>
    /// <param name="lockTimeout">The maximum time spent waiting to acquire the lock.</param>
    public SemaphoreSlimLock(string resource, TimeSpan lockTimeout) : base(resource)
    {
        Timeout = lockTimeout;
    }

    /// <summary>
    /// The ref-counted semaphore entry this lock was acquired from, or <see langword="null"/> when acquisition failed.
    /// </summary>
    internal SemaphoreSlimLockEntry? Entry { get; set; }
}

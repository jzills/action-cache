namespace ActionCache.Common.Concurrency.Locks;

/// <summary>
/// Represents a no-operation cache lock that does not perform any locking mechanism.
/// This is useful as a fallback when no actual locking is required.
/// </summary>
public class NullCacheLock : CacheLock
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NullCacheLock"/> class.
    /// </summary>
    /// <param name="resource">The resource name associated with this lock.</param>
    public NullCacheLock(string resource) : base(resource)
    {
        IsAcquired = true;
    }
}
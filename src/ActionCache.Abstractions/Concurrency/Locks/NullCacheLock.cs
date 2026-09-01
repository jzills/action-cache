namespace ActionCache.Common.Concurrency.Locks;

/// <summary>
/// Represents a no-operation cache lock that always reports itself as acquired without performing any locking.
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
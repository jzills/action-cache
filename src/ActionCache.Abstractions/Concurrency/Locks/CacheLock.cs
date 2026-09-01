namespace ActionCache.Common.Concurrency;

/// <summary>
/// Represents the base class for a cache lock mechanism.
/// </summary>
public abstract class CacheLock
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CacheLock"/> class with a specified resource.
    /// </summary>
    /// <param name="resource">The unique resource identifier to lock.</param>
    public CacheLock(string resource)
    {
        Resource = resource;
    }

    /// <summary>
    /// Gets the unique resource identifier associated with the lock.
    /// </summary>
    public readonly string Resource;

    /// <summary>
    /// Gets or sets a value indicating whether the lock has been successfully acquired.
    /// </summary>
    public bool IsAcquired { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum time allowed for attempting to acquire the lock.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// Gets the UTC timestamp when the lock request was initiated.
    /// </summary>
    public DateTime DateRequested { get; } = DateTime.UtcNow;
}
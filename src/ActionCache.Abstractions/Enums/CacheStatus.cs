namespace ActionCache.Common.Enums;

/// <summary>
/// Specifies the status of a cache operation.
/// </summary>
public enum CacheStatus
{
    /// <summary>
    /// Indicates that a new item was added to the cache.
    /// </summary>
    Add,

    /// <summary>
    /// Indicates that an item was retrieved from the cache.
    /// </summary>
    Hit,

    /// <summary>
    /// Indicates that an item was not found in the cache.
    /// </summary>
    Miss,

    /// <summary>
    /// Indicates that no cache operation was performed.
    /// </summary>
    None,

    /// <summary>
    /// Indicates that an item was removed from the cache.
    /// </summary>
    Evict,

    /// <summary>
    /// Indicates that an item in the cache was refreshed.
    /// </summary>
    Refresh,
}
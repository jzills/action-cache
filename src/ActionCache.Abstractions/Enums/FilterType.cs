namespace ActionCache.Common.Enums;

/// <summary>
/// Specifies the type of cache filter operation.
/// </summary>
public enum FilterType
{
    /// <summary>
    /// Represents an operation that adds an item to the cache.
    /// </summary>
    Add,

    /// <summary>
    /// Represents an operation that removes an item from the cache.
    /// </summary>
    Evict,

    /// <summary>
    /// Represents an operation that refreshes an item in the cache.
    /// </summary>
    Refresh
}
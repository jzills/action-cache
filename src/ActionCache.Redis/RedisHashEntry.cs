namespace ActionCache.Redis;

/// <summary>
/// Represents a collection of constant keys used in Redis hash entries.
/// </summary>
public class RedisHashEntry
{
    /// <summary>
    /// Represents the key for the value stored in the Redis hash.
    /// </summary>
    public const string Value = "VALUE";

    /// <summary>
    /// Represents the key for the absolute expiration time of the Redis entry.
    /// </summary>
    public const string AbsoluteExpiration = "ABSOLUTE_EXPIRATION";

    /// <summary>
    /// Represents the key for the sliding expiration time of the Redis entry.
    /// </summary>
    public const string SlidingExpiration = "SLIDING_EXPIRATION";
}
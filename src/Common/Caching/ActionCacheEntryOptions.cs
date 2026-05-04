namespace ActionCache.Common;

/// <summary>
/// Represents cache entry options for an action cache, allowing configuration of absolute and sliding expirations.
/// </summary>
public class ActionCacheEntryOptions
{
    /// <summary>
    /// Represents a value indicating no expiration for the cache entry.
    /// </summary>
    internal const long NoExpiration = 0;

    /// <summary>
    /// Gets or sets the absolute expiration time as a duration relative to now.
    /// </summary>
    /// <value>The time from now at which the cache entry expires.</value>
    public TimeSpan? AbsoluteExpiration { get; set; }

    /// <summary>
    /// Gets or sets the sliding expiration time, which resets whenever the cache entry is accessed.
    /// </summary>
    /// <value>The sliding expiration duration.</value>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    /// Gets or sets the duration for which the lock will remain valid once acquired.
    /// </summary>
    /// <value>The default is 5 seconds.</value>
    public TimeSpan LockDuration { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum amount of time to wait for acquiring the lock before timing out.
    /// </summary>
    /// <value>The default is 10 seconds.</value>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Calculates the absolute expiration date and time based on <see cref="AbsoluteExpiration"/>, relative to the current UTC time.
    /// </summary>
    /// <returns>A <see cref="DateTimeOffset"/> representing the absolute expiration date and time, or <c>null</c> if <see cref="AbsoluteExpiration"/> is not set.</returns>
    public DateTimeOffset? GetAbsoluteExpirationFromUtcNow() =>
        AbsoluteExpiration.HasValue ?
            DateTimeOffset.UtcNow.Add(AbsoluteExpiration.Value) :
            null;

    /// <summary>
    /// Retrieves the absolute expiration time-to-live (TTL) in milliseconds.
    /// </summary>
    /// <returns>A <c>long</c> representing the TTL in milliseconds, or <see cref="NoExpiration"/> if <see cref="AbsoluteExpiration"/> is not set.</returns>
    public long GetAbsoluteExpirationAsTTLInMilliseconds() =>
        AbsoluteExpiration.HasValue ?
            (long)AbsoluteExpiration.Value.TotalMilliseconds :
            NoExpiration;

    /// <summary>
    /// Calculates the absolute expiration time from the current UTC time, in milliseconds since the Unix epoch.
    /// </summary>
    /// <returns>A <c>long</c> representing the absolute expiration in milliseconds from the Unix epoch, or <see cref="NoExpiration"/> if <see cref="AbsoluteExpiration"/> is not set.</returns>
    public long GetAbsoluteExpirationFromUtcNowInMilliseconds() =>
        GetAbsoluteExpirationFromUtcNow()?.ToUnixTimeMilliseconds() ?? NoExpiration;

    /// <summary>
    /// Retrieves the sliding expiration time in milliseconds.
    /// </summary>
    /// <returns>A <c>long</c> representing the sliding expiration in milliseconds, or <see cref="NoExpiration"/> if <see cref="SlidingExpiration"/> is not set.</returns>
    public long GetSlidingExpirationInMilliseconds() =>
        SlidingExpiration.HasValue ?
            (long)SlidingExpiration.Value.TotalMilliseconds :
            NoExpiration;

    /// <summary>
    /// Determines if the absolute expiration time has passed based on the provided Unix timestamp.
    /// </summary>
    /// <param name="absoluteExpirationUnix">The Unix timestamp (in milliseconds) representing the absolute expiration time.</param>
    /// <returns>
    /// <c>true</c> if the absolute expiration time has passed; otherwise, <c>false</c>.
    /// If <paramref name="absoluteExpirationUnix"/> is less than or equal to <see cref="NoExpiration"/>, expiration is not enforced.
    /// </returns>
    public static bool HasExpiredAbsoluteExpiration(long absoluteExpirationUnix) =>
        HasExpirationValue(absoluteExpirationUnix) &&
        DateTimeOffset.UtcNow >= DateTimeOffset.FromUnixTimeMilliseconds(absoluteExpirationUnix);

    /// <summary>
    /// Determines if a sliding expiration duration is configured for the cache entry.
    /// </summary>
    /// <param name="slidingExpiration">The sliding expiration duration in milliseconds.</param>
    /// <returns>
    /// <c>true</c> if the sliding expiration duration is set (greater than <see cref="NoExpiration"/>); otherwise, <c>false</c>.
    /// </returns>
    public static bool HasSlidingExpiration(long slidingExpiration) => HasExpirationValue(slidingExpiration);

    /// <summary>
    /// Determines if the provided expiration value is valid (greater than the <see cref="NoExpiration"/> value).
    /// </summary>
    /// <param name="value">The expiration value to check.</param>
    /// <returns>
    /// <c>true</c> if the expiration value is greater than <see cref="NoExpiration"/>; otherwise, <c>false</c>.
    /// </returns>
    public static bool HasExpirationValue(long value) => value > NoExpiration;

    /// <summary>
    /// Deconstructs the expiration properties into individual components: absolute expiration, sliding expiration, and time-to-live (TTL).
    /// </summary>
    /// <param name="absoluteExpiration">
    /// The absolute expiration time in milliseconds from the current UTC time.
    /// </param>
    /// <param name="slidingExpiration">
    /// The sliding expiration time in milliseconds, which resets upon each access of the cached entry.
    /// </param>
    /// <param name="ttl">
    /// The time-to-live (TTL) in milliseconds, representing the remaining time before the cache entry expires.
    /// </param>
    public void Deconstruct(out long absoluteExpiration, out long slidingExpiration, out long ttl)
    {
        absoluteExpiration = GetAbsoluteExpirationFromUtcNowInMilliseconds();
        slidingExpiration = GetSlidingExpirationInMilliseconds();
        ttl = HasExpirationValue(slidingExpiration) ? slidingExpiration : GetAbsoluteExpirationAsTTLInMilliseconds();
    }
}
using StackExchange.Redis;

namespace ActionCache.Redis.Extensions.Internal;

/// <summary>
/// Provides extension methods for retrieving values from an array of <see cref="HashEntry"/>.
/// </summary>
internal static class HashEntryArrayExtensions
{
    /// <summary>
    /// Retrieves the Redis value associated with the default hash entry key.
    /// </summary>
    /// <param name="entries">The array of <see cref="HashEntry"/> objects.</param>
    /// <returns>The <see cref="RedisValue"/> associated with the default key.</returns>
    internal static RedisValue GetRedisValue(this HashEntry[] entries) =>
        entries.GetRedisValue(RedisHashEntry.Value);

    /// <summary>
    /// Retrieves the absolute expiration value from the hash entries.
    /// </summary>
    /// <param name="entries">The array of <see cref="HashEntry"/> objects.</param>
    /// <returns>The absolute expiration time as a <see cref="long"/>.</returns>
    internal static long GetAbsoluteExpiration(this HashEntry[] entries) =>
        (long)entries.GetRedisValue(RedisHashEntry.AbsoluteExpiration);

    /// <summary>
    /// Retrieves the sliding expiration value from the hash entries.
    /// </summary>
    /// <param name="entries">The array of <see cref="HashEntry"/> objects.</param>
    /// <returns>The sliding expiration time as a <see cref="long"/>.</returns>
    internal static long GetSlidingExpiration(this HashEntry[] entries) =>
        (long)entries.GetRedisValue(RedisHashEntry.SlidingExpiration);

    /// <summary>
    /// Retrieves the Redis value associated with the specified hash entry key.
    /// </summary>
    /// <param name="entries">The array of <see cref="HashEntry"/> objects.</param>
    /// <param name="name">The name of the hash entry to retrieve.</param>
    /// <returns>The <see cref="RedisValue"/> associated with the specified key.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the specified key does not exist in the entries.</exception>
    private static RedisValue GetRedisValue(this HashEntry[] entries, string name) =>
        entries.First(entry => entry.Name == name).Value;
}
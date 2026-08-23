using ActionCache.Utilities;
using StackExchange.Redis;

namespace ActionCache.Redis;

/// <summary>
/// Represents a RedisNamespace which is a specific type of Namespace.
/// </summary>
public record class RedisNamespace(string Value) : Namespace(Value)
{
    /// <summary>
    /// Implicitly converts a RedisNamespace to a RedisKey.
    /// </summary>
    /// <param name="this">The RedisNamespace to convert.</param>
    /// <returns>A new RedisKey instance.</returns>
    public static implicit operator RedisKey(RedisNamespace @this) => new RedisKey(@this);
    
    /// <summary>
    /// Implicitly converts a string to a RedisNamespace.
    /// </summary>
    /// <param name="namespace">The string to convert.</param>
    /// <returns>A new RedisNamespace instance.</returns>
    public static implicit operator RedisNamespace(string @namespace) => new RedisNamespace(@namespace);
}
using ActionCache.Common.Concurrency;
using ActionCache.Redis.Concurrency;
using ActionCache.Redis.Extensions;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

using ActionCache.Common;

namespace ActionCache.Common.Extensions;

/// <summary>
/// Registers the Redis cache backend.
/// </summary>
/// <remarks>
/// Declared in <c>ActionCache.Common.Extensions</c> — the namespace callers already
/// import for <c>AddActionCache</c> — so existing
/// <c>options.UseRedisCache(...)</c> call sites compile unchanged once this package is
/// referenced.
/// </remarks>
public static class RedisActionCacheOptionsBuilderExtensions
{
    /// <summary>
    /// Enables the Redis cache with the specified configuration.
    /// </summary>
    /// <param name="builder">The options builder.</param>
    /// <param name="configureOptions">Configures the underlying <see cref="RedisCacheOptions"/>.</param>
    /// <returns>The options builder, for chaining.</returns>
    public static ActionCacheOptionsBuilder UseRedisCache(
        this ActionCacheOptionsBuilder builder,
        Action<RedisCacheOptions> configureOptions
    ) => builder
            .AddBackend(services => services.AddActionCacheRedis(configureOptions))
            .AddDistributedLocker(serviceProvider =>
            {
                // Sized from the single-flight options: this locker exists to coalesce
                // misses, and its lease becomes the lock key's TTL. Taking the key-index
                // lock's settings meant a five-second TTL under a ten-second wait, so every
                // action slower than five seconds lost its lock mid-flight and stampeded.
                var singleFlightOptions = serviceProvider
                    .GetRequiredService<ActionCacheSingleFlightOptions>();

                return new RedisCacheLocker(
                    serviceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase(),
                    singleFlightOptions.LeaseDuration,
                    singleFlightOptions.WaitTimeout);
            });

    /// <summary>
    /// Enables the Redis cache with the specified connection configuration.
    /// </summary>
    /// <param name="builder">The options builder.</param>
    /// <param name="configuration">The Redis configuration string.</param>
    /// <returns>The options builder, for chaining.</returns>
    public static ActionCacheOptionsBuilder UseRedisCache(
        this ActionCacheOptionsBuilder builder,
        string configuration
    ) => builder.UseRedisCache(options => options.Configuration = configuration);
}

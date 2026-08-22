using ActionCache.Common.Extensions;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace ActionCache.Redis.Extensions;

/// <summary>
/// Extension methods for adding ActionCache with Redis to the IServiceCollection.
/// </summary>
internal static class IServiceCollectionExtensions
{
    /// <summary>
    /// Adds ActionCache with Redis to the IServiceCollection with custom configuration options.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <param name="configureOptions">An Action to configure the RedisCacheOptions.</param>
    /// <returns>The updated IServiceCollection.</returns>
    internal static IServiceCollection AddActionCacheRedis(
        this IServiceCollection services,
        Action<RedisCacheOptions> configureOptions
    )
    {
        var options = new RedisCacheOptions();
        configureOptions.Invoke(options);

        ArgumentException.ThrowIfNullOrWhiteSpace(options.Configuration);

        var configurationOptions = BuildConfigurationOptions(options.Configuration);

        return services
            .AddActionCacheCommon()
            .AddScoped<IActionCacheFactory, RedisActionCacheFactory>()
            .AddStackExchangeRedisCache(configureOptions)
            .AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(configurationOptions))
            .AddHostedService<RedisExpiryService>();
    }

    /// <summary>
    /// Parses the Redis configuration string and disables <c>AbortOnConnectFail</c> so
    /// that a backend that is unreachable at startup does not prevent the app from
    /// booting; the multiplexer reconnects in the background.
    /// </summary>
    /// <param name="configuration">The Redis configuration string.</param>
    /// <returns>The parsed <see cref="ConfigurationOptions"/> with background reconnect enabled.</returns>
    internal static ConfigurationOptions BuildConfigurationOptions(string configuration)
    {
        var configurationOptions = ConfigurationOptions.Parse(configuration);
        configurationOptions.AbortOnConnectFail = false;
        return configurationOptions;
    }
}
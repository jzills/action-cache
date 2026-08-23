using ActionCache.AzureCosmos;
using ActionCache.Common.Concurrency;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.SqlServer;
using Microsoft.Extensions.Caching.StackExchangeRedis;

namespace ActionCache.Common;

/// <summary>
/// Provides a builder for configuring ActionCache options.
/// </summary>
public class ActionCacheOptionsBuilder
{
    /// <summary>
    /// Stores the options for the ActionCache.
    /// </summary>
    protected readonly ActionCacheOptions Options = new();

    /// <summary>
    /// Configures the entry options for the action cache.
    /// </summary>
    /// <param name="configureOptions">The delegate to configure the entry options.</param>
    /// <returns>Returns this instance of <see cref="ActionCacheOptionsBuilder"/>.</returns>
    public ActionCacheOptionsBuilder UseEntryOptions(Action<ActionCacheEntryOptions> configureOptions)
    {
        configureOptions.Invoke(Options.EntryOptions);
        return this;
    }

    /// <summary>
    /// Enables the use of the memory cache.
    /// </summary>
    /// <returns>Returns this instance of <see cref="ActionCacheOptionsBuilder"/>.</returns>
    public ActionCacheOptionsBuilder UseMemoryCache(Action<MemoryCacheOptions> configureOptions)
    {
        Options.ConfigureMemoryCacheOptions = configureOptions;
        return this;
    }

    /// <summary>
    /// Enables the use of the Redis cache.
    /// </summary>
    /// <returns>Returns this instance of <see cref="ActionCacheOptionsBuilder"/>.</returns>
    public ActionCacheOptionsBuilder UseRedisCache(Action<RedisCacheOptions> configureOptions)
    {
        Options.ConfigureRedisCacheOptions = configureOptions;
        return this;
    }

    /// <summary>
    /// Enables the use of the Redis cache with the specified configuration.
    /// </summary>
    /// <returns>Returns this instance of <see cref="ActionCacheOptionsBuilder"/>.</returns>
    public ActionCacheOptionsBuilder UseRedisCache(string configuration) =>
        UseRedisCache(configureOptions => 
            configureOptions.Configuration = configuration);

    /// <summary>
    /// Enables the use of SQL Server cache.
    /// </summary>
    /// <returns>Returns this instance of <see cref="ActionCacheOptionsBuilder"/>.</returns>
    public ActionCacheOptionsBuilder UseSqlServerCache(Action<SqlServerCacheOptions> configureOptions)
    {
        Options.ConfigureSqlServerCacheOptions = configureOptions;
        return this;
    }

    /// <summary>
    /// Enables the use of Azure Cosmos DB as a cache backend.
    /// </summary>
    /// <returns>Returns this instance of <see cref="ActionCacheOptionsBuilder"/>.</returns>
    public ActionCacheOptionsBuilder UseAzureCosmosCache(Action<AzureCosmosCacheOptions> configureOptions)
    {
        Options.ConfigureAzureCosmosCacheOptions = configureOptions;
        return this;
    }

    /// <summary>
    /// Configures ActionCache to fail closed: cache-backend failures propagate to the
    /// caller instead of being swallowed. By default ActionCache fails open.
    /// </summary>
    /// <param name="failClosed">
    /// <see langword="true"/> (default) to fail closed; <see langword="false"/> to fail open.
    /// </param>
    /// <returns>Returns this instance of <see cref="ActionCacheOptionsBuilder"/>.</returns>
    public ActionCacheOptionsBuilder FailClosed(bool failClosed = true)
    {
        Options.FailClosed = failClosed;
        return this;
    }

    /// <summary>
    /// Bounds how long a single cache-backend operation may take before it is abandoned.
    /// </summary>
    /// <param name="timeout">The maximum duration of one backend operation.</param>
    /// <returns>Returns this instance of <see cref="ActionCacheOptionsBuilder"/>.</returns>
    /// <remarks>
    /// Fail-open catches exceptions; it does not bound a backend that hangs rather than
    /// throws. Without a timeout such a backend hangs the request indefinitely.
    /// </remarks>
    public ActionCacheOptionsBuilder UseOperationTimeout(TimeSpan timeout)
    {
        Options.OperationTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Coordinates single-flight across every instance of the application using the
    /// configured Redis or SQL Server backend's distributed lock, instead of coalescing
    /// only within one process.
    /// </summary>
    /// <returns>Returns this instance of <see cref="ActionCacheOptionsBuilder"/>.</returns>
    /// <remarks>
    /// Every cache miss then costs a lock round-trip to the backend. Requires Redis or SQL
    /// Server to be configured; Redis is preferred when both are.
    /// </remarks>
    public ActionCacheOptionsBuilder UseDistributedSingleFlight(
        Action<ActionCacheSingleFlightOptions>? configureOptions = null)
    {
        Options.UseDistributedSingleFlight = true;
        configureOptions?.Invoke(Options.SingleFlightOptions);
        return this;
    }

    /// <summary>
    /// Configures how concurrent misses for one cache key are coalesced.
    /// </summary>
    /// <param name="configureOptions">The delegate to configure the single-flight options.</param>
    /// <returns>Returns this instance of <see cref="ActionCacheOptionsBuilder"/>.</returns>
    /// <remarks>
    /// Applies to both the in-process default and <see cref="UseDistributedSingleFlight"/>.
    /// Raise <see cref="ActionCacheSingleFlightOptions.LeaseDuration"/> above the slowest
    /// action the cache fronts.
    /// </remarks>
    public ActionCacheOptionsBuilder UseSingleFlightOptions(
        Action<ActionCacheSingleFlightOptions> configureOptions)
    {
        configureOptions.Invoke(Options.SingleFlightOptions);
        return this;
    }

    /// <summary>
    /// Builds the configured <see cref="ActionCacheOptions"/>.
    /// </summary>
    /// <returns>The configured <see cref="ActionCacheOptions"/>.</returns>
    internal ActionCacheOptions Build() => Options;
}
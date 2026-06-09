using ActionCache.AzureCosmos;
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
    /// Builds the configured <see cref="ActionCacheOptions"/>.
    /// </summary>
    /// <returns>The configured <see cref="ActionCacheOptions"/>.</returns>
    internal ActionCacheOptions Build() => Options;
}
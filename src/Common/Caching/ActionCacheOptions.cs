using ActionCache.AzureCosmos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.SqlServer;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using ActionCache.Common.Concurrency;

namespace ActionCache.Common;

/// <summary>
/// Provides configuration options for action caching.
/// </summary>
public class ActionCacheOptions
{
    /// <summary>
    /// Gets the default entry options for the cache.
    /// </summary>
    public readonly ActionCacheEntryOptions EntryOptions = new();

    /// <summary>
    /// Gets or sets a value indicating whether cache-backend failures propagate to
    /// the caller (fail-closed). Defaults to <see langword="false"/> (fail-open).
    /// </summary>
    public bool FailClosed { get; set; }

    /// <summary>
    /// Gets or sets the maximum time a single cache-backend operation may take before it is
    /// abandoned. <see langword="null"/> (default) imposes no timeout.
    /// </summary>
    public TimeSpan? OperationTimeout { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether single-flight coalescing is coordinated
    /// across every instance of the application using a backend's distributed lock,
    /// rather than only within one process. Defaults to <see langword="false"/>.
    /// </summary>
    public bool UseDistributedSingleFlight { get; set; }

    /// <summary>
    /// Gets the options controlling how concurrent misses for one key are coalesced.
    /// </summary>
    public ActionCacheSingleFlightOptions SingleFlightOptions { get; } = new();

    /// <summary>
    /// Gets or sets a delegate to configure options for <see cref="MemoryCacheOptions"/>.
    /// </summary>
    public Action<MemoryCacheOptions>? ConfigureMemoryCacheOptions { get; set; }

    /// <summary>
    /// Gets or sets a delegate to configure options for <see cref="RedisCacheOptions"/>.
    /// </summary>
    public Action<RedisCacheOptions>? ConfigureRedisCacheOptions { get; set; }

    /// <summary>
    /// Gets or sets a delegate to configure options for <see cref="SqlServerCacheOptions"/>.
    /// </summary>
    public Action<SqlServerCacheOptions>? ConfigureSqlServerCacheOptions { get; set; }

    /// <summary>
    /// Gets or sets a delegate to configure options for <see cref="AzureCosmosCacheOptions"/>.
    /// </summary>
    public Action<AzureCosmosCacheOptions>? ConfigureAzureCosmosCacheOptions { get; set; }
}
using ActionCache.AzureCosmos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.SqlServer;
using Microsoft.Extensions.Caching.StackExchangeRedis;

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
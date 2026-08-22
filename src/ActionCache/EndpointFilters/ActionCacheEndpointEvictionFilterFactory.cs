using ActionCache.Common;
using ActionCache.Common.Enums;
using Microsoft.AspNetCore.Http;

namespace ActionCache.Filters;

/// <summary>
/// A filter factory attribute that creates an <see cref="ActionCache.EndpointFilters.ActionCacheEndpointEvictionFilter"/> to evict cached entries for the configured namespace.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ActionCacheEndpointEvictionFilterFactory : ActionCacheEndpointFilterFactoryBase
{
    /// <summary>
    /// The absolute expiration in milliseconds for a cache entry.
    /// </summary>
    /// <value>Defaults to 0 which represents no expiration.</value>
    public long AbsoluteExpiration { get; set; } = ActionCacheEntryOptions.NoExpiration;

    /// <summary>
    /// The sliding expiration in milliseconds for a cache entry.
    /// </summary>
    /// <value>Defaults to 0 which represents no expiration.</value>
    public long SlidingExpiration { get; set; } = ActionCacheEntryOptions.NoExpiration;

    /// <summary>
    /// Creates an instance of the action cache filter using the specified service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve dependencies.</param>
    /// <returns>An instance of an action cache filter.</returns>
    public override IEndpointFilter CreateInstance(IServiceProvider serviceProvider) =>
        CreateInstance(serviceProvider,
            FilterType.Evict,
            TimeSpan.FromMilliseconds(AbsoluteExpiration),
            TimeSpan.FromMilliseconds(SlidingExpiration)
        );
}
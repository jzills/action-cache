using ActionCache.Common;
using ActionCache.Common.Enums;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ActionCache.Filters;

/// <summary>
/// Provides a custom filter factory for caching action results based on the configuration.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ActionCacheFilterFactory : ActionCacheFilterFactoryBase
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
    /// Whether concurrent misses for one cache key are coalesced so the action executes once.
    /// </summary>
    /// <value>
    /// Defaults to <see langword="true"/>. Set to <see langword="false"/> to let every
    /// concurrent miss execute the action, which is only appropriate when the action has
    /// per-request side effects that must not be skipped.
    /// </value>
    public bool SingleFlight { get; set; } = true;

    /// <summary>
    /// Creates an instance of the action cache filter using the specified service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve dependencies.</param>
    /// <returns>An instance of an action cache filter.</returns>
    public override IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        CreateInstance(serviceProvider,
            FilterType.Add,
            TimeSpan.FromMilliseconds(AbsoluteExpiration),
            TimeSpan.FromMilliseconds(SlidingExpiration),
            SingleFlight
        );
}
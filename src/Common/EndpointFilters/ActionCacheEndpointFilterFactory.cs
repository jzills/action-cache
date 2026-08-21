using ActionCache.Common;
using ActionCache.Common.Enums;
using ActionCache.Common.Keys.VaryBy;
using Microsoft.AspNetCore.Http;

namespace ActionCache.Filters;

/// <summary>
/// Provides a custom filter factory for caching action results based on the configuration.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ActionCacheEndpointFilterFactory : ActionCacheEndpointFilterFactoryBase
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
    /// Whether the authenticated user's identity forms part of the cache key.
    /// </summary>
    /// <value>
    /// Defaults to <see cref="VaryByUserMode.Auto"/>: authenticated requests get per-user
    /// cache entries automatically, which is what stops one user's response being served
    /// to another. Set to <see cref="VaryByUserMode.Never"/> for a response that genuinely
    /// does not depend on who asked.
    /// </value>
    public VaryByUserMode VaryByUser { get; set; } = VaryByUserMode.Auto;

    /// <summary>
    /// A comma-separated list of request header names to vary the cache key by.
    /// </summary>
    public string? VaryByHeader { get; set; }

    /// <summary>
    /// A comma-separated list of query-string keys to vary the cache key by.
    /// </summary>
    public string? VaryByQuery { get; set; }

    /// <summary>
    /// A comma-separated list of claim types to vary the cache key by.
    /// </summary>
    public string? VaryByClaim { get; set; }

    /// <summary>
    /// Collects this attribute's vary-by settings.
    /// </summary>
    /// <returns>The vary-by options declared here.</returns>
    internal VaryByOptions GetVaryByOptions() => new()
    {
        User = VaryByUser,
        Headers = VaryByHeader,
        Query = VaryByQuery,
        Claims = VaryByClaim
    };

    /// <summary>
    /// Whether concurrent misses for one cache key are coalesced so the endpoint executes once.
    /// </summary>
    /// <value>
    /// Defaults to <see langword="true"/>. Set to <see langword="false"/> to let every
    /// concurrent miss execute the endpoint, which is only appropriate when the endpoint has
    /// per-request side effects that must not be skipped.
    /// </value>
    public bool SingleFlight { get; set; } = true;

    /// <summary>
    /// Creates an instance of the action cache filter using the specified service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve dependencies.</param>
    /// <returns>An instance of an action cache filter.</returns>
    public override IEndpointFilter CreateInstance(IServiceProvider serviceProvider) =>
        CreateInstance(serviceProvider,
            FilterType.Add,
            TimeSpan.FromMilliseconds(AbsoluteExpiration),
            TimeSpan.FromMilliseconds(SlidingExpiration),
            SingleFlight,
            GetVaryByOptions()
        );
}
using System.Diagnostics.CodeAnalysis;
using ActionCache.Attributes;
using ActionCache.Common;
using ActionCache.Filters;
using ActionCache.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ActionCache.EndpointFilters.Extensions;

/// <summary>
/// Extension methods for adding action cache and eviction behavior to <see cref="RouteHandlerBuilder"/>.
/// </summary>
/// <remarks>
/// Each extension captures its namespace in the filter's closure rather than reading it back
/// from endpoint metadata. <c>GetMetadata&lt;T&gt;()</c> returns the *last* match, so two
/// chained calls would both resolve to the second namespace -- evicting one namespace twice
/// and the other never. The attributes are still written as metadata, but only so the startup
/// validator can see every declaration on an endpoint.
/// </remarks>
public static class RouteHandlerBuilderExtensions
{
    /// <summary>
    /// Adds action cache behavior to the specified route handler using the provided namespace.
    /// </summary>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/> to extend.</param>
    /// <param name="namespace">The route namespace used for caching context.</param>
    /// <returns>The modified <see cref="RouteHandlerBuilder"/> with caching enabled.</returns>
    public static RouteHandlerBuilder WithActionCache(this RouteHandlerBuilder builder, [StringSyntax("Route")] Namespace @namespace) =>
        builder.WithActionCache(@namespace, static _ => { });

    /// <summary>
    /// Adds action cache behavior to the specified route handler, configuring the same
    /// per-endpoint settings <c>[ActionCache]</c> exposes on a controller action.
    /// </summary>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/> to extend.</param>
    /// <param name="namespace">The route namespace used for caching context.</param>
    /// <param name="configure">Configures expiration, vary-by and single-flight for this endpoint.</param>
    /// <returns>The modified <see cref="RouteHandlerBuilder"/> with caching enabled.</returns>
    /// <example>
    /// <code>
    /// app.MapGet("/forecasts", () =&gt; repository.All())
    ///    .WithActionCache("Forecasts", options =&gt;
    ///    {
    ///        options.AbsoluteExpiration = TimeSpan.FromMinutes(5);
    ///        options.VaryByQuery = "page,size";
    ///    });
    /// </code>
    /// </example>
    public static RouteHandlerBuilder WithActionCache(
        this RouteHandlerBuilder builder,
        [StringSyntax("Route")] Namespace @namespace,
        Action<ActionCacheEndpointOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        // Read once here rather than per request: the options describe the endpoint, which
        // does not change between requests, and running caller code on every request would
        // make an expensive lambda an expensive endpoint.
        var options = new ActionCacheEndpointOptions();
        configure(options);

        return builder.WithMetadata(new ActionCacheAttribute { Namespace = @namespace })
            .AddEndpointFilter((context, next) =>
            {
                var endpointFilterFactory = new ActionCacheEndpointFilterFactory
                {
                    Namespace = @namespace,
                    AbsoluteExpiration = ToMilliseconds(options.AbsoluteExpiration),
                    SlidingExpiration = ToMilliseconds(options.SlidingExpiration),
                    VaryByUser = options.VaryByUser,
                    VaryByHeader = options.VaryByHeader,
                    VaryByQuery = options.VaryByQuery,
                    VaryByClaim = options.VaryByClaim,
                    SingleFlight = options.SingleFlight
                };

                var endpointFilter = endpointFilterFactory.CreateInstance(context.HttpContext.RequestServices);
                return endpointFilter.InvokeAsync(context, next);
            });
    }

    /// <summary>
    /// Converts an optional expiration to the milliseconds the filter factory states them in.
    /// </summary>
    /// <param name="expiration">The configured expiration, or <see langword="null"/> for none.</param>
    /// <returns>The expiration in milliseconds, or <see cref="ActionCacheEntryOptions.NoExpiration"/>.</returns>
    private static long ToMilliseconds(TimeSpan? expiration) =>
        expiration is { } value ? (long)value.TotalMilliseconds : ActionCacheEntryOptions.NoExpiration;

    /// <summary>
    /// Adds action cache eviction behavior to the specified route handler using the provided namespace.
    /// </summary>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/> to extend.</param>
    /// <param name="namespace">The route namespace used for identifying cached content to evict.</param>
    /// <returns>The modified <see cref="RouteHandlerBuilder"/> with cache eviction enabled.</returns>
    public static RouteHandlerBuilder WithActionCacheEviction(this RouteHandlerBuilder builder, [StringSyntax("Route")] Namespace @namespace) =>
        builder.WithMetadata(new ActionCacheEvictionAttribute { Namespace = @namespace })
            .AddEndpointFilter((context, next) =>
            {
                var endpointFilterFactory = new ActionCacheEndpointEvictionFilterFactory { Namespace = @namespace };
                var endpointFilter = endpointFilterFactory.CreateInstance(context.HttpContext.RequestServices);
                return endpointFilter.InvokeAsync(context, next);
            });

    /// <summary>
    /// Adds action cache refresh behavior to the specified route handler using the provided namespace.
    /// </summary>
    /// <remarks>
    /// Refresh replays the request recorded on each entry in the namespace, so the cache is
    /// left warm rather than empty. Entries whose key varied by the request -- by user, header,
    /// query or claim -- are skipped, since replaying another caller's request would mean
    /// impersonating them.
    /// </remarks>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/> to extend.</param>
    /// <param name="namespace">The route namespace whose entries are refreshed.</param>
    /// <returns>The modified <see cref="RouteHandlerBuilder"/> with cache refresh enabled.</returns>
    public static RouteHandlerBuilder WithActionCacheRefresh(this RouteHandlerBuilder builder, [StringSyntax("Route")] Namespace @namespace) =>
        builder.WithMetadata(new ActionCacheRefreshAttribute { Namespace = @namespace })
            .AddEndpointFilter((context, next) =>
            {
                var endpointFilterFactory = new ActionCacheEndpointRefreshFilterFactory { Namespace = @namespace };
                var endpointFilter = endpointFilterFactory.CreateInstance(context.HttpContext.RequestServices);
                return endpointFilter.InvokeAsync(context, next);
            });
}

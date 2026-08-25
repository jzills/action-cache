using System.Diagnostics.CodeAnalysis;
using ActionCache.Attributes;
using ActionCache.Filters;
using ActionCache.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ActionCache.EndpointFilters.Extensions;

/// <summary>
/// Extension methods for adding action cache and eviction behavior to <see cref="RouteHandlerBuilder"/>.
/// </summary>
public static class RouteHandlerBuilderExtensions
{
    /// <summary>
    /// An instance of <see cref="EndpointFilterInvocationContextSource"/> used to extract metadata attributes. 
    /// </summary>
    private static readonly EndpointFilterInvocationContextSource Source = new();

    /// <summary>
    /// Adds action cache behavior to the specified route handler using the provided namespace.
    /// </summary>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/> to extend.</param>
    /// <param name="namespace">The route namespace used for caching context.</param>
    /// <returns>The modified <see cref="RouteHandlerBuilder"/> with caching enabled.</returns>
    public static RouteHandlerBuilder WithActionCache(this RouteHandlerBuilder builder, [StringSyntax("Route")] Namespace @namespace) =>
        builder.WithMetadata(new ActionCacheAttribute { Namespace = @namespace })
            .AddEndpointFilter((context, next) =>
            {
                if (Source.TryGetValue<ActionCacheAttribute>(context, out var attribute))
                {
                    var endpointFilterFactory = new ActionCacheEndpointFilterFactory { Namespace = attribute.Namespace };
                    var endpointFilter = endpointFilterFactory.CreateInstance(context.HttpContext.RequestServices);
                    return endpointFilter.InvokeAsync(context, next);
                }
                else
                {
                    return next(context);
                }
            });

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
                if (Source.TryGetValue<ActionCacheEvictionAttribute>(context, out var attribute))
                {
                    var endpointFilterFactory = new ActionCacheEndpointEvictionFilterFactory { Namespace = attribute.Namespace };
                    var endpointFilter = endpointFilterFactory.CreateInstance(context.HttpContext.RequestServices);
                    return endpointFilter.InvokeAsync(context, next);
                }
                else
                {
                    return next(context);
                }
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
                if (Source.TryGetValue<ActionCacheRefreshAttribute>(context, out var attribute))
                {
                    var endpointFilterFactory = new ActionCacheEndpointRefreshFilterFactory { Namespace = attribute.Namespace };
                    var endpointFilter = endpointFilterFactory.CreateInstance(context.HttpContext.RequestServices);
                    return endpointFilter.InvokeAsync(context, next);
                }
                else
                {
                    return next(context);
                }
            });

    /// <summary>
    /// Helper class to extract metadata attributes from <see cref="EndpointFilterInvocationContext"/>.
    /// </summary>
    private class EndpointFilterInvocationContextSource
    {
        /// <summary>
        /// Tries to retrieve a metadata attribute of the specified type from the endpoint context.
        /// </summary>
        /// <typeparam name="T">The type of attribute to retrieve.</typeparam>
        /// <param name="context">The filter invocation context.</param>
        /// <param name="attribute">When this method returns, contains the attribute if found; otherwise, <c>null</c>.</param>
        /// <returns><c>true</c> if the attribute was found; otherwise, <c>false</c>.</returns>
        internal bool TryGetValue<T>(EndpointFilterInvocationContext context, [NotNullWhen(true)] out T? attribute) where T : Attribute
        {
            var endpoint = context.HttpContext.GetEndpoint();
            if (endpoint is null)
            {
                attribute = null;
                return false;
            }
            else
            {
                attribute = endpoint.Metadata.GetMetadata<T>();
                return attribute is not null;
            }
        }
    }
}
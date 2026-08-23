using ActionCache.Common.Caching;
using ActionCache.Common.Concurrency;
using ActionCache.Common.Enums;
using ActionCache.Common.Keys.VaryBy;
using ActionCache.Common.Responses;
using ActionCache.EndpointFilters;
using ActionCache.Exceptions;
using ActionCache.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Logging;

namespace ActionCache.Common.Filters;

/// <summary>
/// The abstract factory for creating Minimal API endpoint cache filters.
/// </summary>
public class ActionCacheEndpointFilterAbstractFactory : ActionCacheFilterAbstractFactoryBase<IEndpointFilter>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCacheEndpointFilterAbstractFactory"/> class.
    /// </summary>
    /// <param name="cacheFactories">The cache factories used to create caches.</param>
    /// <param name="binderFactory">The template binder for parsing route parameters for templated namespaces.</param>
    /// <param name="resilientDecorator">Wraps created caches for graceful degradation.</param>
    /// <param name="loggerFactory">The factory used to create loggers for the filters this factory produces.</param>
    /// <param name="singleFlight">Coalesces concurrent misses for the same key.</param>
    /// <param name="varyByResolver">Resolves the request dimensions that form part of the cache key.</param>
    /// <param name="responseFactory">Converts between endpoint results and stored responses.</param>
    public ActionCacheEndpointFilterAbstractFactory(
        IEnumerable<IActionCacheFactory> cacheFactories,
        TemplateBinderFactory binderFactory,
        ResilientCacheDecorator resilientDecorator,
        ILoggerFactory loggerFactory,
        IActionCacheSingleFlight singleFlight,
        ActionCacheVaryByResolver varyByResolver,
        CachedResponseFactory responseFactory
    ) : base(cacheFactories, binderFactory, resilientDecorator, loggerFactory, singleFlight, varyByResolver, responseFactory)
    {
    }

    /// <inheritdoc/>
    internal override IEndpointFilter CreateFilter(ActionCacheHandler cache, FilterType type, bool singleFlight, VaryByOptions varyByOptions) =>
        type switch
        {
            FilterType.Add      => new ActionCacheEndpointFilter(cache, BinderFactory, LoggerFactory.CreateLogger<ActionCacheEndpointFilter>(), SingleFlight, singleFlight, VaryByResolver, varyByOptions, ResponseFactory),
            FilterType.Evict    => new ActionCacheEndpointEvictionFilter(cache, BinderFactory, LoggerFactory.CreateLogger<ActionCacheEndpointEvictionFilter>()),
            _                   => throw new FilterTypeNotSupportedException(type)
        };
}

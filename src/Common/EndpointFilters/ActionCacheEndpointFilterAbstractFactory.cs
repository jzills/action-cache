using ActionCache.Common.Caching;
using ActionCache.Common.Concurrency;
using ActionCache.Common.Enums;
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
    public ActionCacheEndpointFilterAbstractFactory(
        IEnumerable<IActionCacheFactory> cacheFactories,
        TemplateBinderFactory binderFactory,
        ResilientCacheDecorator resilientDecorator,
        ILoggerFactory loggerFactory,
        IActionCacheSingleFlight singleFlight
    ) : base(cacheFactories, binderFactory, resilientDecorator, loggerFactory, singleFlight)
    {
    }

    /// <inheritdoc/>
    internal override IEndpointFilter CreateFilter(ActionCacheHandler cache, FilterType type, bool singleFlight) =>
        type switch
        {
            FilterType.Add      => new ActionCacheEndpointFilter(cache, BinderFactory, LoggerFactory.CreateLogger<ActionCacheEndpointFilter>(), SingleFlight, singleFlight),
            FilterType.Evict    => new ActionCacheEndpointEvictionFilter(cache, BinderFactory, LoggerFactory.CreateLogger<ActionCacheEndpointEvictionFilter>()),
            _                   => throw new FilterTypeNotSupportedException(type)
        };
}

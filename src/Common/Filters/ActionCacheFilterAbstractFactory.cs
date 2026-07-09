using ActionCache.Common.Caching;
using ActionCache.Common.Enums;
using ActionCache.Exceptions;
using ActionCache.Filters;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Logging;

namespace ActionCache.Common.Filters;

/// <summary>
/// The abstract factory for creating MVC cache filters.
/// </summary>
public class ActionCacheFilterAbstractFactory : ActionCacheFilterAbstractFactoryBase<IFilterMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCacheFilterAbstractFactory"/> class.
    /// </summary>
    /// <param name="cacheFactories">The cache factories used to create caches.</param>
    /// <param name="binderFactory">The template binder for parsing route parameters for templated namespaces.</param>
    /// <param name="resilientDecorator">Wraps created caches for graceful degradation.</param>
    /// <param name="loggerFactory">The factory used to create loggers for the filters this factory produces.</param>
    public ActionCacheFilterAbstractFactory(
        IEnumerable<IActionCacheFactory> cacheFactories,
        TemplateBinderFactory binderFactory,
        ResilientCacheDecorator resilientDecorator,
        ILoggerFactory loggerFactory
    ) : base(cacheFactories, binderFactory, resilientDecorator, loggerFactory)
    {
    }

    /// <inheritdoc/>
    internal override IFilterMetadata CreateFilter(ActionCacheHandler cache, FilterType type) =>
        type switch
        {
            FilterType.Add      => new ActionCacheFilter(cache, BinderFactory, LoggerFactory.CreateLogger<ActionCacheFilter>()),
            FilterType.Evict    => new ActionCacheEvictionFilter(cache, BinderFactory, LoggerFactory.CreateLogger<ActionCacheEvictionFilter>()),
            FilterType.Refresh  => new ActionCacheRefreshFilter(cache, BinderFactory, LoggerFactory.CreateLogger<ActionCacheRefreshFilter>()),
            _                   => throw new FilterTypeNotSupportedException(type)
        };
}

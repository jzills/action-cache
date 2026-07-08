using ActionCache.Common.Caching;
using ActionCache.Common.Enums;
using ActionCache.Exceptions;
using ActionCache.Filters;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing.Template;

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
    public ActionCacheFilterAbstractFactory(
        IEnumerable<IActionCacheFactory> cacheFactories,
        TemplateBinderFactory binderFactory,
        ResilientCacheDecorator resilientDecorator
    ) : base(cacheFactories, binderFactory, resilientDecorator)
    {
    }

    /// <inheritdoc/>
    internal override IFilterMetadata CreateFilter(ActionCacheHandler cache, FilterType type) =>
        type switch
        {
            FilterType.Add      => new ActionCacheFilter(cache, BinderFactory),
            FilterType.Evict    => new ActionCacheEvictionFilter(cache, BinderFactory),
            FilterType.Refresh  => new ActionCacheRefreshFilter(cache, BinderFactory),
            _                   => throw new FilterTypeNotSupportedException(type)
        };
}

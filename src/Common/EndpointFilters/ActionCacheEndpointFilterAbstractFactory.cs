using ActionCache.Common.Caching;
using ActionCache.Common.Enums;
using ActionCache.EndpointFilters;
using ActionCache.Exceptions;
using ActionCache.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;

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
    public ActionCacheEndpointFilterAbstractFactory(
        IEnumerable<IActionCacheFactory> cacheFactories,
        TemplateBinderFactory binderFactory,
        ResilientCacheDecorator resilientDecorator
    ) : base(cacheFactories, binderFactory, resilientDecorator)
    {
    }

    /// <inheritdoc/>
    internal override IEndpointFilter CreateFilter(ActionCacheHandler cache, FilterType type) =>
        type switch
        {
            FilterType.Add      => new ActionCacheEndpointFilter(cache, BinderFactory),
            FilterType.Evict    => new ActionCacheEndpointEvictionFilter(cache, BinderFactory),
            _                   => throw new FilterTypeNotSupportedException(type)
        };
}

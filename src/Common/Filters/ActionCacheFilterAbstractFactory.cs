using ActionCache.Common.Caching;
using ActionCache.Common.Enums;
using ActionCache.Common.Extensions.Internal;
using ActionCache.Exceptions;
using ActionCache.Filters;
using ActionCache.Utilities;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing.Template;

namespace ActionCache.Common.Filters;

/// <summary>
/// The abstract factory for creating cache filters.
/// </summary>
public class ActionCacheFilterAbstractFactory : IActionCacheFilterAbstractFactory<IFilterMetadata>
{
    /// <summary>
    /// The cache factories used to create caches.
    /// </summary>
    protected readonly IEnumerable<IActionCacheFactory> CacheFactories;

    /// <summary>
    /// The template binder for parsing route parameters for templated namespaces.
    /// </summary>
    protected readonly TemplateBinderFactory BinderFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCacheFilterAbstractFactory"/> class.
    /// </summary>
    /// <param name="cacheFactories">The cache factories used to create caches.</param>
    /// <param name="binderFactory">The template binder for parsing route parameters for templated namespaces.</param>
    public ActionCacheFilterAbstractFactory(
        IEnumerable<IActionCacheFactory> cacheFactories,
        TemplateBinderFactory binderFactory
    )
    {
        CacheFactories = cacheFactories;
        BinderFactory = binderFactory;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidCacheInstanceException">Thrown if no cache instances could be created for the namespace.</exception>
    /// <exception cref="FilterTypeNotSupportedException">Thrown if the specified filter type is not supported.</exception>
    public IFilterMetadata CreateInstance(Namespace @namespace, FilterType type) =>
        CreateInstance(@namespace, absoluteExpiration: null, slidingExpiration: null, type);

    /// <inheritdoc/>
    /// <exception cref="InvalidCacheInstanceException">Thrown if no cache instances could be created for the namespace.</exception>
    /// <exception cref="FilterTypeNotSupportedException">Thrown if the specified filter type is not supported.</exception>
    public IFilterMetadata CreateInstance(Namespace @namespace, TimeSpan? absoluteExpiration, TimeSpan? slidingExpiration, FilterType type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(@namespace, nameof(@namespace));

        var caches = GetCacheInstances(@namespace, absoluteExpiration, slidingExpiration);
        return CreateHandler(caches, type);
    }

    /// <summary>
    /// Creates an <see cref="IFilterMetadata"/> handler for the specified caches and filter type.
    /// </summary>
    /// <param name="caches">A read-only list of action cache instances to handle.</param>
    /// <param name="type">The type of filter to create.</param>
    /// <returns>An <see cref="IFilterMetadata"/> implementation based on the specified filter type.</returns>
    /// <exception cref="InvalidCacheInstanceException">Thrown if no cache instances are provided.</exception>
    internal IFilterMetadata CreateHandler(IReadOnlyList<IActionCache> caches, FilterType type)
    {
        if (caches.Count == 0)
        {
            throw new InvalidCacheInstanceException($"No cache instances were able to be created for type \"{type}\".");
        } 
        else
        {
            var cacheHandler = new ActionCacheHandler(caches.First());
            foreach (var cache in caches.Skip(1))
            {
                cacheHandler.SetNext(cache);
            } 

            return CreateFilter(cacheHandler, type);
        }
    }

    /// <summary>
    /// Creates a filter based on the specified cache handler and filter type.
    /// </summary>
    /// <param name="cache">The cache handler to use for the filter.</param>
    /// <param name="type">The type of filter to create.</param>
    /// <returns>An <see cref="IFilterMetadata"/> implementation corresponding to the filter type.</returns>
    /// <exception cref="FilterTypeNotSupportedException">Thrown if the filter type is unsupported.</exception>
    internal IFilterMetadata CreateFilter(ActionCacheHandler cache, FilterType type) => 
        type switch
        {
            FilterType.Add      => new ActionCacheFilter(cache, BinderFactory),
            FilterType.Evict    => new ActionCacheEvictionFilter(cache, BinderFactory),
            FilterType.Refresh  => new ActionCacheRefreshFilter(cache, BinderFactory),   
            _                   => throw new FilterTypeNotSupportedException(type)         
        };

    /// <summary>
    /// Retrieves cache instances for a specified namespace and optional expiration settings.
    /// </summary>
    /// <param name="namespace">The namespace for which to retrieve cache instances.</param>
    /// <param name="absoluteExpiration">Optional absolute expiration time for the cache instances.</param>
    /// <param name="slidingExpiration">Optional sliding expiration time for the cache instances.</param>
    /// <returns>A read-only list of action cache instances.</returns>
    internal IReadOnlyList<IActionCache> GetCacheInstances(Namespace @namespace,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null
    )
    {
        List<IActionCache> cacheInstances = [];

        if (((string)@namespace).Contains(","))
        {
            foreach (var value in ((string)@namespace).SplitNamespace())
            {
                AddCacheInstances(value, cacheInstances, absoluteExpiration, slidingExpiration);
            }
        }
        else
        {
            AddCacheInstances(@namespace, cacheInstances, absoluteExpiration, slidingExpiration);
        }

        return cacheInstances.AsReadOnly();
    }

    /// <summary>
    /// Adds cache instances for a given namespace to the provided list.
    /// </summary>
    /// <param name="namespace">The namespace for which to add cache instances.</param>
    /// <param name="cacheInstances">A list to which the created cache instances will be added.</param>
    /// <param name="absoluteExpiration">Optional absolute expiration time for the cache instances.</param>
    /// <param name="slidingExpiration">Optional sliding expiration time for the cache instances.</param>
    /// <exception cref="InvalidCacheInstanceException">Thrown if the created instances are null or invalid.</exception>
    internal void AddCacheInstances(Namespace @namespace, 
        in List<IActionCache> cacheInstances,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null
    )
    {
        var instances = CreateCacheInstances(@namespace, absoluteExpiration, slidingExpiration);
        if (instances is null || instances.Any(instance => instance is null))
        {
            throw new InvalidCacheInstanceException();
        }
        else
        {
            cacheInstances.AddRange(instances!);
        }
    }

    /// <summary>
    /// Creates cache instances for a given namespace with optional expiration settings.
    /// </summary>
    /// <param name="namespace">The namespace for which to create cache instances.</param>
    /// <param name="absoluteExpiration">Optional absolute expiration time for the cache instances.</param>
    /// <param name="slidingExpiration">Optional sliding expiration time for the cache instances.</param>
    /// <returns>A collection of cache instances or null if creation fails.</returns>
    internal IEnumerable<IActionCache?>? CreateCacheInstances( 
        Namespace @namespace,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null
    )
    {
        Func<IActionCacheFactory, IActionCache?> selector = 
            (absoluteExpiration, slidingExpiration) switch
            {
                // If no expiration is specified, fallback to default 
                // entry options specified during configuration.
                (null, null) => factory => factory.Create(@namespace),
                _            => factory => factory.Create(@namespace, absoluteExpiration, slidingExpiration)
            };
       
        return CacheFactories.Select(selector);
    }
}
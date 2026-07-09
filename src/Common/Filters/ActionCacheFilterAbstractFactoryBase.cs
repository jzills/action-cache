using ActionCache.Common.Caching;
using ActionCache.Common.Enums;
using ActionCache.Common.Extensions.Internal;
using ActionCache.Exceptions;
using ActionCache.Utilities;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Logging;

namespace ActionCache.Common.Filters;

/// <summary>
/// Shared base for the MVC and Minimal API abstract filter factories. Holds the backend
/// cache factories, the route/template binder, and the resilience decorator, and implements
/// namespace resolution and cache-instance creation once; subclasses provide only the
/// concrete filter via <see cref="CreateFilter"/>.
/// </summary>
/// <typeparam name="TFilter">The filter abstraction produced (MVC <c>IFilterMetadata</c> or Minimal API <c>IEndpointFilter</c>).</typeparam>
public abstract class ActionCacheFilterAbstractFactoryBase<TFilter> : IActionCacheFilterAbstractFactory<TFilter>
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
    /// Decorates created caches so backend failures degrade gracefully.
    /// </summary>
    protected readonly ResilientCacheDecorator ResilientDecorator;

    /// <summary>
    /// The factory used to create loggers for the filters this factory produces.
    /// </summary>
    protected readonly ILoggerFactory LoggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCacheFilterAbstractFactoryBase{TFilter}"/> class.
    /// </summary>
    /// <param name="cacheFactories">The cache factories used to create caches.</param>
    /// <param name="binderFactory">The template binder for parsing route parameters for templated namespaces.</param>
    /// <param name="resilientDecorator">Wraps created caches for graceful degradation.</param>
    /// <param name="loggerFactory">The factory used to create loggers for the filters this factory produces.</param>
    protected ActionCacheFilterAbstractFactoryBase(
        IEnumerable<IActionCacheFactory> cacheFactories,
        TemplateBinderFactory binderFactory,
        ResilientCacheDecorator resilientDecorator,
        ILoggerFactory loggerFactory
    )
    {
        CacheFactories = cacheFactories;
        BinderFactory = binderFactory;
        ResilientDecorator = resilientDecorator;
        LoggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidCacheInstanceException">Thrown if no cache instances could be created for the namespace.</exception>
    /// <exception cref="FilterTypeNotSupportedException">Thrown if the specified filter type is not supported.</exception>
    public TFilter CreateInstance(Namespace @namespace, FilterType type) =>
        CreateInstance(@namespace, absoluteExpiration: null, slidingExpiration: null, type);

    /// <inheritdoc/>
    /// <exception cref="InvalidCacheInstanceException">Thrown if no cache instances could be created for the namespace.</exception>
    /// <exception cref="FilterTypeNotSupportedException">Thrown if the specified filter type is not supported.</exception>
    public TFilter CreateInstance(Namespace @namespace, TimeSpan? absoluteExpiration, TimeSpan? slidingExpiration, FilterType type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(@namespace, nameof(@namespace));

        var caches = GetCacheInstances(@namespace, absoluteExpiration, slidingExpiration);
        return CreateHandler(caches, type);
    }

    /// <summary>
    /// Creates the concrete filter for the specified cache handler and filter type.
    /// Implemented by each subclass to produce the MVC or Minimal API filter.
    /// </summary>
    /// <param name="cache">The cache handler to use for the filter.</param>
    /// <param name="type">The type of filter to create.</param>
    /// <returns>A filter implementation corresponding to the filter type.</returns>
    /// <exception cref="FilterTypeNotSupportedException">Thrown if the filter type is unsupported.</exception>
    internal abstract TFilter CreateFilter(ActionCacheHandler cache, FilterType type);

    /// <summary>
    /// Chains the specified caches into a handler and creates the corresponding filter.
    /// </summary>
    /// <param name="caches">A read-only list of action cache instances to handle.</param>
    /// <param name="type">The type of filter to create.</param>
    /// <returns>A filter implementation based on the specified filter type.</returns>
    /// <exception cref="InvalidCacheInstanceException">Thrown if no cache instances are provided.</exception>
    internal TFilter CreateHandler(IReadOnlyList<IActionCache> caches, FilterType type)
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
    /// Adds resilience-decorated cache instances for a given namespace to the provided list.
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
        // Materialize once so each factory's Create runs a single time — the null-check
        // and the resilience wrap below then operate on the same instances.
        var instances = CreateCacheInstances(@namespace, absoluteExpiration, slidingExpiration)?.ToList();
        if (instances is null || instances.Any(instance => instance is null))
        {
            throw new InvalidCacheInstanceException();
        }
        else
        {
            cacheInstances.AddRange(instances.Select(cache => ResilientDecorator.Decorate(cache!)));
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

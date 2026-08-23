using ActionCache.Common.Concurrency;
using ActionCache.Common.Diagnostics;
using ActionCache.Common.Keys;
using ActionCache.Common.Keys.VaryBy;
using ActionCache.Common.Responses;
using ActionCache.Common.Extensions.Internal;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Logging;

namespace ActionCache.Filters;

/// <summary>
/// The abstract base class for <see cref="ActionCacheFilter"/>.
/// </summary>
public abstract class ActionCacheFilterBase
{
    /// <summary>
    /// An instance of an implementation of an IActionCache.
    /// </summary>
    protected readonly IActionCache Cache;

    /// <summary>
    /// The template binder for parsing route parameters for templated namespaces.
    /// </summary>
    protected readonly TemplateBinderFactory BinderFactory;

    /// <summary>
    /// Coalesces concurrent misses for the same key.
    /// </summary>
    protected readonly IActionCacheSingleFlight SingleFlight;

    /// <summary>
    /// Whether this endpoint opted into single-flight. When <see langword="false"/> the
    /// filter takes the direct path and never touches <see cref="SingleFlight"/>.
    /// </summary>
    protected readonly bool SingleFlightEnabled;

    /// <summary>
    /// Resolves the request dimensions that form part of the cache key.
    /// </summary>
    protected readonly ActionCacheVaryByResolver VaryByResolver;

    /// <summary>
    /// Which request dimensions this endpoint varies its cache key by.
    /// </summary>
    protected readonly VaryByOptions VaryByOptions;

    /// <summary>
    /// Converts between endpoint results and the responses stored in a backend.
    /// </summary>
    protected readonly CachedResponseFactory ResponseFactory;

    /// <summary>
    /// Controls how cache keys are formed.
    /// </summary>
    protected readonly ActionCacheKeyOptions KeyOptions;

    /// <summary>
    /// The logger used to record filter-level conditions the cache layer cannot observe.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCacheFilterBase"/> class with the specified cache, template binder factory, and logger.
    /// </summary>
    /// <param name="cache">The <see cref="IActionCache"/> instance used for caching actions.</param>
    /// <param name="binderFactory">The <see cref="TemplateBinderFactory"/> instance used for binding route templates.</param>
    /// <param name="logger">The logger used to record filter-level conditions the cache layer cannot observe.</param>
    /// <param name="singleFlight">Coalesces concurrent misses for the same key.</param>
    /// <param name="singleFlightEnabled">Whether this endpoint opted into single-flight.</param>
    /// <param name="varyByResolver">Resolves the request dimensions that form part of the cache key.</param>
    /// <param name="varyByOptions">Which request dimensions this endpoint varies its cache key by.</param>
    /// <param name="responseFactory">Converts between endpoint results and stored responses.</param>
    /// <param name="keyOptions">Controls how cache keys are formed.</param>
    internal ActionCacheFilterBase(
        IActionCache cache,
        TemplateBinderFactory binderFactory,
        ILogger logger,
        IActionCacheSingleFlight singleFlight,
        bool singleFlightEnabled,
        ActionCacheVaryByResolver varyByResolver,
        VaryByOptions varyByOptions,
        CachedResponseFactory responseFactory,
        ActionCacheKeyOptions keyOptions)
    {
        Cache = cache;
        BinderFactory = binderFactory;
        _logger = logger;
        SingleFlight = singleFlight;
        SingleFlightEnabled = singleFlightEnabled;
        VaryByResolver = varyByResolver;
        VaryByOptions = varyByOptions;
        ResponseFactory = responseFactory;
        KeyOptions = keyOptions;
    }

    /// <summary>
    /// Initializes a new instance for a filter that never produces cache entries — eviction
    /// and refresh — and so has nothing to coalesce.
    /// </summary>
    /// <param name="cache">The <see cref="IActionCache"/> instance used for caching actions.</param>
    /// <param name="binderFactory">The <see cref="TemplateBinderFactory"/> instance used for binding route templates.</param>
    /// <param name="logger">The logger used to record filter-level conditions the cache layer cannot observe.</param>
    internal ActionCacheFilterBase(IActionCache cache, TemplateBinderFactory binderFactory, ILogger logger)
        : this(cache, binderFactory, logger, NullActionCacheSingleFlight.Instance, singleFlightEnabled: false,
               ActionCacheVaryByResolver.None, new VaryByOptions(), CachedResponseFactory.None,
               new ActionCacheKeyOptions())
    {
    }

    /// <summary>
    /// Attaches any route values to a namespace that contains route template placeholders.
    /// </summary>
    /// <param name="routeValues">A dictionary of route values.</param>
    protected void AttachRouteValues(RouteValueDictionary routeValues)
    {
        var @namespace = Cache.GetNamespace();
        @namespace.AttachRouteValues(routeValues, BinderFactory);
    }

    /// <summary>
    /// Records that no cache key could be constructed for the current request, so it executed uncached.
    /// </summary>
    protected void LogCacheKeyUnavailable()
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            ActionCacheLog.FilterCacheKeyUnavailable(_logger, GetType().Name, (string)Cache.GetNamespace());
        }
    }

    /// <summary>
    /// Records that the action produced a result that was not cacheable, so no entry was stored.
    /// </summary>
    protected void LogResultNotCacheable()
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            ActionCacheLog.FilterResultNotCacheable(_logger, GetType().Name, (string)Cache.GetNamespace());
        }
    }

    /// <summary>
    /// Records that this request evicted its namespace.
    /// </summary>
    /// <remarks>
    /// Recorded here, once per request, rather than on <c>ResilientActionCache</c>. That
    /// decorator wraps each backend individually and <c>ActionCacheHandler</c> fans a
    /// namespace eviction out to every layer, so one <c>[ActionCacheEviction]</c> request
    /// against a Memory + Redis + SQL chain published three evictions — the published count
    /// was of backend calls, not of evictions. This is the same move
    /// <c>actioncache.requests</c> already made off that decorator.
    ///
    /// The tag is the unresolved namespace template, matching every other instrument; a
    /// comma-separated multi-namespace attribute is counted once, under the first of them.
    /// </remarks>
    protected void RecordEviction() =>
        ActionCacheDiagnostics.Evictions.Add(1,
            new KeyValuePair<string, object?>("namespace", Cache.GetNamespace().Value));
}
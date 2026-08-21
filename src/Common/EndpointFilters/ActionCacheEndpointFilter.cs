using ActionCache.Common.Concurrency;
using ActionCache.Common.Enums;
using ActionCache.Common.Extensions;
using ActionCache.Common.Extensions.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using ActionCache.MinimalApi.Extensions.Internal;
using Microsoft.Extensions.Logging;

namespace ActionCache.Filters;

/// <summary>
/// Represents a filter to cache results for improving performance.
/// </summary>
public class ActionCacheEndpointFilter : ActionCacheFilterBase, IEndpointFilter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCacheEndpointFilter"/> class.
    /// </summary>
    /// <param name="cache">The cache implementation to use.</param>
    /// <param name="binderFactory">The binder used for namespaces with route templates.</param>
    /// <param name="logger">The logger used to record filter-level conditions the cache layer cannot observe.</param>
    /// <param name="singleFlight">Coalesces concurrent misses for the same key.</param>
    /// <param name="singleFlightEnabled">Whether this endpoint opted into single-flight.</param>
    public ActionCacheEndpointFilter(
        IActionCache cache,
        TemplateBinderFactory binderFactory,
        ILogger logger,
        IActionCacheSingleFlight singleFlight,
        bool singleFlightEnabled
    ) : base(cache, binderFactory, logger, singleFlight, singleFlightEnabled)
    {
    }

    /// <summary>
    /// Executes the cache filter logic. Attempts to retrieve the response from cache; if not found, proceeds to the next filter or endpoint,
    /// caches the result if available, and returns it.
    /// </summary>
    /// <param name="context">The <see cref="EndpointFilterInvocationContext"/> for the current request.</param>
    /// <param name="next">The delegate to invoke the next filter or endpoint in the pipeline.</param>
    /// <returns>The cached or newly generated response.</returns>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var cancellationToken = context.HttpContext.RequestAborted;

        AttachRouteValues(context.HttpContext.GetRouteData().Values);

        if (!context.TryGetKey(out var key))
        {
            context.AddCacheStatus(CacheStatus.Miss);
            LogCacheKeyUnavailable();
            return await next(context);
        }

        var cacheValue = await Cache.GetAsync<object?>(key, cancellationToken);
        if (cacheValue is not null)
        {
            context.AddCacheStatus(CacheStatus.Hit);
            return cacheValue;
        }

        if (!SingleFlightEnabled)
        {
            return await ExecuteAndCacheAsync(context, next, key, cancellationToken);
        }

        var outcome = await SingleFlight.GetOrCreateAsync<object?>(
            Cache.GetNamespace(),
            key,
            cacheReader: () => Cache.GetAsync<object?>(key, cancellationToken),
            valueFactory: () => ExecuteAndCacheAsync(context, next, key, cancellationToken));

        if (outcome.WasCoalesced)
        {
            context.AddCacheStatus(CacheStatus.Hit);
        }

        return outcome.Value;
    }

    /// <summary>
    /// Executes the endpoint and stores its result when the result is cacheable.
    /// </summary>
    /// <param name="context">The endpoint filter invocation context.</param>
    /// <param name="next">The endpoint filter delegate.</param>
    /// <param name="key">The cache key for this request.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The endpoint's result.</returns>
    private async Task<object?> ExecuteAndCacheAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next,
        string key,
        CancellationToken cancellationToken)
    {
        var result = await next(context);
        if (result.IsSuccessfulEndpointResult())
        {
            context.AddCacheStatus(CacheStatus.Add);
            await Cache.SetAsync(key, result, cancellationToken);
        }
        else
        {
            context.AddCacheStatus(CacheStatus.None);
            LogResultNotCacheable();
        }

        return result;
    }
}

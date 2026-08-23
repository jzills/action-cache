using ActionCache.Common.Concurrency;
using ActionCache.Common.Enums;
using ActionCache.Common.Extensions;
using ActionCache.Common.Extensions.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Logging;

namespace ActionCache.Filters;

/// <summary>
/// Represents a filter to cache action results for improving performance.
/// </summary>
public class ActionCacheFilter : ActionCacheFilterBase, IAsyncActionFilter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCacheFilter"/> class.
    /// </summary>
    /// <param name="cache">The cache implementation to use.</param>
    /// <param name="binderFactory">The binder used for namespaces with route templates.</param>
    /// <param name="logger">The logger used to record filter-level conditions the cache layer cannot observe.</param>
    /// <param name="singleFlight">Coalesces concurrent misses for the same key.</param>
    /// <param name="singleFlightEnabled">Whether this endpoint opted into single-flight.</param>
    public ActionCacheFilter(
        IActionCache cache,
        TemplateBinderFactory binderFactory,
        ILogger logger,
        IActionCacheSingleFlight singleFlight,
        bool singleFlightEnabled
    ) : base(cache, binderFactory, logger, singleFlight, singleFlightEnabled)
    {
    }

    /// <summary>
    /// Called asynchronously before the action, after model binding is complete.
    /// </summary>
    /// <param name="context">The action executing context.</param>
    /// <param name="next">The action execution delegate. Invoked to execute the next action filter or the action itself.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        AttachRouteValues(context.RouteData.Values);

        if (!context.TryGetKey(out var key))
        {
            context.AddCacheStatus(CacheStatus.Miss);
            LogCacheKeyUnavailable();
            await next();
            return;
        }

        var cacheValue = await Cache.GetAsync<IActionResult?>(key);
        if (cacheValue is not null)
        {
            context.AddCacheStatus(CacheStatus.Hit);
            context.Result = cacheValue;
            return;
        }

        if (!SingleFlightEnabled)
        {
            await ExecuteAndCacheAsync(context, next, key);
            return;
        }

        var result = await SingleFlight.GetOrCreateAsync<IActionResult?>(
            Cache.GetNamespace(),
            key,
            cacheReader: () => Cache.GetAsync<IActionResult?>(key),
            valueFactory: async () =>
            {
                await ExecuteAndCacheAsync(context, next, key);

                // The leader's result is already in the pipeline via next(); returning null
                // keeps it from being mistaken for a coalesced value.
                return null;
            });

        if (result.WasCoalesced)
        {
            context.AddCacheStatus(CacheStatus.Hit);
            context.Result = result.Value;
        }
    }

    /// <summary>
    /// Executes the action and stores its result when the result is cacheable.
    /// </summary>
    /// <param name="context">The action executing context.</param>
    /// <param name="next">The action execution delegate.</param>
    /// <param name="key">The cache key for this request.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ExecuteAndCacheAsync(ActionExecutingContext context, ActionExecutionDelegate next, string key)
    {
        var actionExecutedContext = await next();
        if (actionExecutedContext.Result is not null &&
            actionExecutedContext.Result.IsCacheableResult())
        {
            context.AddCacheStatus(CacheStatus.Add);
            await Cache.SetAsync(key, actionExecutedContext.Result);
        }
        else
        {
            context.AddCacheStatus(CacheStatus.None);
            LogResultNotCacheable();
        }
    }
}

using ActionCache.Common.Caching;
using ActionCache.Common.Concurrency;
using ActionCache.Common.Diagnostics;
using ActionCache.Common.Enums;
using ActionCache.Common.Keys;
using ActionCache.Common.Keys.VaryBy;
using ActionCache.Common.Responses;
using ActionCache.Common.Extensions;
using ActionCache.Common.Extensions.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
    /// <param name="varyByResolver">Resolves the request dimensions that form part of the cache key.</param>
    /// <param name="varyByOptions">Which request dimensions this endpoint varies its cache key by.</param>
    /// <param name="responseFactory">Converts between action results and stored responses.</param>
    /// <param name="keyOptions">Controls how cache keys are formed.</param>
    public ActionCacheFilter(
        IActionCache cache,
        TemplateBinderFactory binderFactory,
        ILogger logger,
        IActionCacheSingleFlight singleFlight,
        bool singleFlightEnabled,
        ActionCacheVaryByResolver varyByResolver,
        VaryByOptions varyByOptions,
        CachedResponseFactory responseFactory,
        ActionCacheKeyOptions keyOptions
    ) : base(cache, binderFactory, logger, singleFlight, singleFlightEnabled, varyByResolver, varyByOptions, responseFactory, keyOptions)
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
        var cancellationToken = context.HttpContext.RequestAborted;

        // A refresh replay must reach the action: serving it from cache would hand it the
        // stale entry it exists to replace. The refresh loop stores what it produces.
        if (ActionCacheReplayMarker.IsReplay(context.HttpContext))
        {
            await next();
            return;
        }

        AttachRouteValues(context.RouteData.Values);

        var varyByValues = await VaryByResolver.ResolveAsync(context.HttpContext, VaryByOptions, cancellationToken);

        if (!context.TryGetKey(out var key, varyByValues, KeyOptions))
        {
            context.AddCacheStatus(CacheStatus.Miss);
            // Counted, so the instrument totals requests rather than only the ones that got
            // as far as a key. A request the cache could not serve is a miss.
            RecordLookup(CacheStatus.Miss);
            LogCacheKeyUnavailable();
            await next();
            return;
        }

        var cacheValue = await Cache.GetAsync<CachedResponse>(key, cancellationToken);
        if (cacheValue is not null)
        {
            context.AddCacheStatus(CacheStatus.Hit);
            RecordLookup(CacheStatus.Hit);
            context.Result = CachedResponseFactory.ToActionResult(cacheValue);
            return;
        }

        if (!SingleFlightEnabled)
        {
            RecordLookup(CacheStatus.Miss);
            await ExecuteAndCacheAsync(context, next, key, varyByValues.Count > 0, cancellationToken);
            return;
        }

        var result = await SingleFlight.GetOrCreateAsync<CachedResponse>(
            Cache.GetNamespace(),
            key,
            cacheReader: () => Cache.GetAsync<CachedResponse>(key, cancellationToken),
            valueFactory: async () =>
            {
                await ExecuteAndCacheAsync(context, next, key, varyByValues.Count > 0, cancellationToken);

                // The leader's result is already in the pipeline via next(); returning null
                // keeps it from being mistaken for a coalesced value.
                return null;
            });

        var coalescedHit = result.WasCoalesced && result.Value is not null;
        if (coalescedHit)
        {
            context.AddCacheStatus(CacheStatus.Hit);
            context.Result = CachedResponseFactory.ToActionResult(result.Value!);
        }

        // A coalesced waiter was served without the action running, which is a hit by the
        // only definition that matters to the ratio.
        RecordLookup(coalescedHit ? CacheStatus.Hit : CacheStatus.Miss);
    }

    /// <summary>
    /// Executes the action and stores its result when the result is cacheable.
    /// </summary>
    /// <param name="context">The action executing context.</param>
    /// <param name="next">The action execution delegate.</param>
    /// <param name="key">The cache key for this request.</param>
    /// <param name="variesByRequest">Whether the cache key varies by request context.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ExecuteAndCacheAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next,
        string key,
        bool variesByRequest,
        CancellationToken cancellationToken)
    {
        var actionExecutedContext = await next();
        if (actionExecutedContext.Result is not null &&
            actionExecutedContext.Result.IsCacheableResult() &&
            ResponseFactory.TryCreate(
                actionExecutedContext.Result,
                ResponseFactory.CreateRequest(context.HttpContext, GetBoundBody(context, variesByRequest)),
                variesByRequest,
                out var cachedResponse))
        {
            context.AddCacheStatus(CacheStatus.Add);
            await Cache.SetAsync(key, cachedResponse, cancellationToken);
        }
        else
        {
            context.AddCacheStatus(CacheStatus.None);
            LogResultNotCacheable();
        }
    }

    /// <summary>
    /// Finds the argument bound from the request body, if the action takes one.
    /// </summary>
    /// <param name="context">The action executing context.</param>
    /// <param name="variesByRequest">Whether the cache key varies by request context.</param>
    /// <returns>The bound body model, or <see langword="null"/> when there is none to record.</returns>
    /// <remarks>
    /// Refresh replays the recorded request, so an action with a <c>[FromBody]</c> parameter
    /// has to carry its payload or the replay binds nothing and the endpoint answers 415 —
    /// overwriting a good cache entry with an error.
    /// </remarks>
    private static object? GetBoundBody(ActionExecutingContext context, bool variesByRequest)
    {
        // An entry that varies by request is skipped by refresh outright, so its payload
        // could never be replayed — persisting it would put request bodies, which is where
        // credentials and PII live, in the cache store for nothing. Since VaryByUserMode.Auto
        // that is every authenticated endpoint. See ActionCacheBase.TryRefreshKeyAsync.
        if (variesByRequest || context.ActionDescriptor is not ControllerActionDescriptor descriptor)
        {
            return null;
        }

        foreach (var parameter in descriptor.Parameters)
        {
            if (parameter.BindingInfo?.BindingSource == BindingSource.Body &&
                context.ActionArguments.TryGetValue(parameter.Name, out var value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Records the outcome of this request's cache lookup.
    /// </summary>
    /// <param name="status">The outcome.</param>
    private void RecordLookup(CacheStatus status) =>
        ActionCacheDiagnostics.RecordRequest(
            Cache.GetNamespace().Value,
            status == CacheStatus.Hit ? "hit" : "miss");
}

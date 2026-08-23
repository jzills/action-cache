using ActionCache.Common.Caching;
using ActionCache.Common.Concurrency;
using ActionCache.Common.Diagnostics;
using ActionCache.Common.Enums;
using ActionCache.Common.Keys;
using ActionCache.Common.Keys.VaryBy;
using ActionCache.Common.Responses;
using ActionCache.Common.Extensions;
using ActionCache.Common.Extensions.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
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
    /// <param name="varyByResolver">Resolves the request dimensions that form part of the cache key.</param>
    /// <param name="varyByOptions">Which request dimensions this endpoint varies its cache key by.</param>
    /// <param name="responseFactory">Converts between endpoint results and stored responses.</param>
    /// <param name="keyOptions">Controls how cache keys are formed.</param>
    public ActionCacheEndpointFilter(
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
    /// Executes the cache filter logic. Attempts to retrieve the response from cache; if not found, proceeds to the next filter or endpoint,
    /// caches the result if available, and returns it.
    /// </summary>
    /// <param name="context">The <see cref="EndpointFilterInvocationContext"/> for the current request.</param>
    /// <param name="next">The delegate to invoke the next filter or endpoint in the pipeline.</param>
    /// <returns>The cached or newly generated response.</returns>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var cancellationToken = context.HttpContext.RequestAborted;

        // A refresh replay must reach the endpoint: serving it from cache would hand it the
        // stale entry it exists to replace. The refresh loop stores what it produces.
        if (ActionCacheReplayMarker.IsReplay(context.HttpContext))
        {
            return await next(context);
        }

        AttachRouteValues(context.HttpContext.GetRouteData().Values);

        var varyByValues = await VaryByResolver.ResolveAsync(context.HttpContext, VaryByOptions, cancellationToken);

        if (!context.TryGetKey(out var key, varyByValues, KeyOptions))
        {
            context.AddCacheStatus(CacheStatus.Miss);
            // Counted, so the instrument totals requests rather than only the ones that got
            // as far as a key. A request the cache could not serve is a miss.
            RecordLookup(CacheStatus.Miss);
            LogCacheKeyUnavailable();
            return await next(context);
        }

        var cacheValue = await Cache.GetAsync<CachedResponse>(key, cancellationToken);
        if (cacheValue is not null)
        {
            context.AddCacheStatus(CacheStatus.Hit);
            RecordLookup(CacheStatus.Hit);
            return CachedResponseFactory.ToEndpointResult(cacheValue);
        }

        if (!SingleFlightEnabled)
        {
            RecordLookup(CacheStatus.Miss);
            return await ExecuteAndCacheAsync(context, next, key, varyByValues.Count > 0, cancellationToken);
        }

        var outcome = await SingleFlight.GetOrCreateAsync<object?>(
            Cache.GetNamespace(),
            key,
            cacheReader: async () =>
            {
                var cached = await Cache.GetAsync<CachedResponse>(key, cancellationToken);
                return cached is null ? null : CachedResponseFactory.ToEndpointResult(cached);
            },
            valueFactory: () => ExecuteAndCacheAsync(context, next, key, varyByValues.Count > 0, cancellationToken));

        if (outcome.WasCoalesced)
        {
            context.AddCacheStatus(CacheStatus.Hit);
        }

        // A coalesced waiter was served without the endpoint running, which is a hit by the
        // only definition that matters to the ratio.
        RecordLookup(outcome.WasCoalesced ? CacheStatus.Hit : CacheStatus.Miss);
        return outcome.Value;
    }

    /// <summary>
    /// Executes the endpoint and stores its result when the result is cacheable.
    /// </summary>
    /// <param name="context">The endpoint filter invocation context.</param>
    /// <param name="next">The endpoint filter delegate.</param>
    /// <param name="key">The cache key for this request.</param>
    /// <param name="variesByRequest">Whether the cache key varies by request context.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The endpoint's result.</returns>
    private async Task<object?> ExecuteAndCacheAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next,
        string key,
        bool variesByRequest,
        CancellationToken cancellationToken)
    {
        var result = await next(context);
        if (result.IsSuccessfulEndpointResult() &&
            ResponseFactory.TryCreateFromEndpointResult(
                result,
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

        return result;
    }

    /// <summary>
    /// Finds the argument bound from the request body, if the endpoint takes one.
    /// </summary>
    /// <param name="context">The endpoint filter invocation context.</param>
    /// <param name="variesByRequest">Whether the cache key varies by request context.</param>
    /// <returns>The bound body model, or <see langword="null"/> when there is none to record.</returns>
    /// <remarks>
    /// The MVC filter reads the body parameter off the action descriptor; Minimal APIs have
    /// no descriptor, so the endpoint's <see cref="IAcceptsMetadata"/> names the type the
    /// framework bound from the body and the matching argument is that model. Without this
    /// a cached <c>MapPost</c> recorded no body at all, and its replay — sent bodyless —
    /// was answered 400 or 415, so refresh could never replace the entry.
    /// </remarks>
    private static object? GetBoundBody(EndpointFilterInvocationContext context, bool variesByRequest)
    {
        // An entry that varies by request is skipped by refresh outright, so its payload
        // could never be replayed — persisting it would put request bodies in the cache
        // store for nothing. See ActionCacheBase.TryRefreshKeyAsync.
        if (variesByRequest)
        {
            return null;
        }

        var requestType = context.HttpContext.GetEndpoint()?
            .Metadata.GetMetadata<IAcceptsMetadata>()?.RequestType;

        if (requestType is null)
        {
            return null;
        }

        foreach (var argument in context.Arguments)
        {
            if (argument is not null && requestType.IsInstanceOfType(argument))
            {
                return argument;
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

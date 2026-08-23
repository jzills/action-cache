using System.Reflection;
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
    /// no descriptor. <see cref="IAcceptsMetadata"/> names the type bound from the body, but
    /// the type alone does not identify the argument: in
    /// <c>MapPost("/echo/{name}", (string name, [FromBody] string payload) =&gt; ...)</c> the
    /// request type is <c>string</c> and so is the route value, and matching on type picked
    /// <c>name</c> — refresh then replayed the route value as the payload and overwrote a
    /// good entry with a response computed for different input, under the original key.
    ///
    /// The handler's <see cref="MethodInfo"/> is in the endpoint metadata, and its parameters
    /// line up positionally with <see cref="EndpointFilterInvocationContext.Arguments"/>, so
    /// the body parameter can be identified rather than guessed. When it cannot be — no
    /// method metadata, or more than one equally plausible candidate — this records nothing.
    /// Refresh then skips the entry and says so, which is the safe direction: a wrong body
    /// silently corrupts the entry, a missing one only leaves it stale.
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

        var endpoint = context.HttpContext.GetEndpoint();
        var requestType = endpoint?.Metadata.GetMetadata<IAcceptsMetadata>()?.RequestType;
        if (requestType is null)
        {
            return null;
        }

        var index = FindBodyParameterIndex(endpoint!, requestType, context);
        if (index is not null)
        {
            return context.Arguments[index.Value];
        }

        // No usable method metadata. Fall back to matching on type, but only when exactly
        // one argument matches — the ambiguous case is what produced the wrong body.
        object? match = null;
        foreach (var argument in context.Arguments)
        {
            if (argument is null || !requestType.IsInstanceOfType(argument))
            {
                continue;
            }

            if (match is not null)
            {
                return null;
            }

            match = argument;
        }

        return match;
    }

    /// <summary>
    /// Locates the handler parameter bound from the request body.
    /// </summary>
    /// <param name="endpoint">The endpoint being invoked.</param>
    /// <param name="requestType">The type <see cref="IAcceptsMetadata"/> names for the body.</param>
    /// <param name="context">The endpoint filter invocation context.</param>
    /// <returns>
    /// The index into <see cref="EndpointFilterInvocationContext.Arguments"/>, or
    /// <see langword="null"/> when the body parameter cannot be identified unambiguously.
    /// </returns>
    private static int? FindBodyParameterIndex(
        Endpoint endpoint,
        Type requestType,
        EndpointFilterInvocationContext context)
    {
        var parameters = endpoint.Metadata.GetMetadata<MethodInfo>()?.GetParameters();

        // Arguments are positional against the handler's parameters. If the counts disagree
        // the two cannot be lined up, so nothing here can be trusted.
        if (parameters is null || parameters.Length != context.Arguments.Count)
        {
            return null;
        }

        // An explicit [FromBody] settles it outright, whatever the types involved.
        var explicitBody = IndexOfSingle(parameters, static parameter =>
            parameter.GetCustomAttributes(inherit: false).OfType<IFromBodyMetadata>().Any());

        if (explicitBody is not null)
        {
            return explicitBody;
        }

        // Otherwise the body is the parameter of the accepted type that nothing else claims:
        // not bound from route, query, header, form or services, and not named after a route
        // token — an unattributed parameter matching a route token binds from the route.
        var routeValues = context.HttpContext.Request.RouteValues;

        return IndexOfSingle(parameters, parameter =>
            requestType.IsAssignableFrom(parameter.ParameterType) &&
            !HasNonBodyBindingSource(parameter) &&
            (parameter.Name is null || !routeValues.ContainsKey(parameter.Name)));
    }

    /// <summary>
    /// Returns the index of the only parameter satisfying <paramref name="predicate"/>.
    /// </summary>
    /// <param name="parameters">The handler's parameters.</param>
    /// <param name="predicate">The test to apply.</param>
    /// <returns>The single matching index, or <see langword="null"/> for none or several.</returns>
    private static int? IndexOfSingle(ParameterInfo[] parameters, Func<ParameterInfo, bool> predicate)
    {
        int? found = null;
        for (var index = 0; index < parameters.Length; index++)
        {
            if (!predicate(parameters[index]))
            {
                continue;
            }

            if (found is not null)
            {
                return null;
            }

            found = index;
        }

        return found;
    }

    /// <summary>
    /// Whether the parameter declares a binding source other than the request body.
    /// </summary>
    /// <param name="parameter">The handler parameter.</param>
    /// <returns><see langword="true"/> when something other than the body binds it.</returns>
    private static bool HasNonBodyBindingSource(ParameterInfo parameter) =>
        parameter.GetCustomAttributes(inherit: false).Any(attribute =>
            attribute is IFromRouteMetadata or IFromQueryMetadata or IFromHeaderMetadata
                      or IFromFormMetadata or IFromServiceMetadata);

    /// <summary>
    /// Records the outcome of this request's cache lookup.
    /// </summary>
    /// <param name="status">The outcome.</param>
    private void RecordLookup(CacheStatus status) =>
        ActionCacheDiagnostics.RecordRequest(
            Cache.GetNamespace().Value,
            status == CacheStatus.Hit ? "hit" : "miss");
}

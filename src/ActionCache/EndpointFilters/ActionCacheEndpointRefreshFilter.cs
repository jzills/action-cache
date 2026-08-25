using ActionCache.Common.Caching;
using ActionCache.Common.Enums;
using ActionCache.Common.Extensions;
using ActionCache.MinimalApi.Extensions.Internal;
using ActionCache.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Logging;

namespace ActionCache.EndpointFilters;

/// <summary>
/// An endpoint filter that refreshes every entry in a namespace after a successful response,
/// leaving the cache warm rather than empty.
/// </summary>
/// <remarks>
/// The Minimal API counterpart of <see cref="ActionCacheRefreshFilter"/>. Nothing about the
/// refresh itself is specific to either hosting model: the recorded request is replayed
/// against the endpoint resolved from <see cref="Microsoft.AspNetCore.Routing.EndpointDataSource"/>,
/// which is how a controller action is dispatched too.
/// </remarks>
public class ActionCacheEndpointRefreshFilter : ActionCacheFilterBase, IEndpointFilter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCacheEndpointRefreshFilter"/> class.
    /// </summary>
    /// <param name="cache">The cache service used for refreshing cache entries.</param>
    /// <param name="binderFactory">The template binder for parsing route parameters for templated namespaces.</param>
    /// <param name="logger">The logger used to record filter-level conditions the cache layer cannot observe.</param>
    public ActionCacheEndpointRefreshFilter(
        IActionCache cache,
        TemplateBinderFactory binderFactory,
        ILogger logger
    ) : base(cache, binderFactory, logger)
    {
    }

    /// <summary>
    /// Executes the endpoint and then refreshes the namespace when the result is successful.
    /// </summary>
    /// <param name="context">The <see cref="EndpointFilterInvocationContext"/> for the current request.</param>
    /// <param name="next">The delegate to invoke the next filter or endpoint in the pipeline.</param>
    /// <returns>The result the endpoint produced, unchanged.</returns>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);

        // A refresh replay must not start a refresh of its own. The pass replays an entry,
        // the replay lands here, and refreshing again replays the same entry -- it does not
        // terminate.
        if (!ActionCacheReplayMarker.IsReplay(context.HttpContext) && result.IsSuccessfulEndpointResult())
        {
            AttachRouteValues(context.HttpContext.GetRouteData().Values);

            // Deliberately not RequestAborted, matching the MVC refresh filter and both
            // eviction filters. RefreshAsync checks the token between keys, so a client that
            // hangs up mid-pass would leave the namespace half-refreshed -- and the write
            // this is attached to has already succeeded.
            await Cache.RefreshAsync();
            context.AddCacheStatus(CacheStatus.Refresh);
        }

        return result;
    }
}

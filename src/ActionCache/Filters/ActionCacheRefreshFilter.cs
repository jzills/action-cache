using ActionCache.Common.Caching;
using ActionCache.Common.Enums;
using ActionCache.Common.Extensions;
using ActionCache.Common.Extensions.Internal;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Logging;

namespace ActionCache.Filters;

/// <summary>
/// Provides a filter for refreshing action caches.
/// </summary>
internal class ActionCacheRefreshFilter : ActionCacheFilterBase, IAsyncResultFilter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCacheRefreshFilter"/> class.
    /// </summary>
    /// <param name="cache">The cache service used for refreshing cache entries.</param>
    /// <param name="binderFactory">The template binder for parsing route parameters for templated namespaces.</param>
    /// <param name="logger">The logger used to record filter-level conditions the cache layer cannot observe.</param>
    public ActionCacheRefreshFilter(
        IActionCache cache,
        TemplateBinderFactory binderFactory,
        ILogger logger
    ) : base(cache, binderFactory, logger)
    {
    }
    
    /// <summary>
    /// Asynchronously executes the result operation with cache refresh.
    /// </summary>
    /// <param name="context">The context for result executing.</param>
    /// <param name="next">Delegate for the result execution.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task OnResultExecutionAsync(
        ResultExecutingContext context, 
        ResultExecutionDelegate next
    )
    {
        // A refresh replay must not start a refresh of its own. The pass replays an entry,
        // the replay lands here, and refreshing again replays the same entry -- it does not
        // terminate. The replay still executes its result; the refresh loop stores it.
        if (!ActionCacheReplayMarker.IsReplay(context.HttpContext) && context.Result.IsSuccessfulResult())
        {
            AttachRouteValues(context.RouteData.Values);
            
            await Cache.RefreshAsync();
            context.AddCacheStatus(CacheStatus.Refresh);
        }
        
        await next();
    }
}
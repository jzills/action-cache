using ActionCache.Common.Enums;
using ActionCache.Common.Extensions;
using ActionCache.Common.Extensions.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing.Template;

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
    public ActionCacheFilter(
        IActionCache cache, 
        TemplateBinderFactory binderFactory
    ) : base(cache, binderFactory)
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

        if (context.TryGetKey(out var key))
        {
            var cacheValue = await Cache.GetAsync<IActionResult?>(key);
            if (cacheValue is not null)
            {
                context.AddCacheStatus(CacheStatus.Hit);
                context.Result = cacheValue;
            }
            else
            {
                var actionExecutedContext = await next();
                if (actionExecutedContext.Result is not null &&
                    actionExecutedContext.Result.IsSuccessfulResult())
                {
                    context.AddCacheStatus(CacheStatus.Add);
                    await Cache.SetAsync(key, actionExecutedContext.Result);
                }
                else
                {
                    context.AddCacheStatus(CacheStatus.None);
                }
            }
        }
        else
        {
            context.AddCacheStatus(CacheStatus.Miss);
            await next();
        }
    }
}
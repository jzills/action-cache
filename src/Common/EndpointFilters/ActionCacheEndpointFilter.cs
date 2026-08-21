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
    public ActionCacheEndpointFilter(
        IActionCache cache,
        TemplateBinderFactory binderFactory,
        ILogger logger
    ) : base(cache, binderFactory, logger)
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
        AttachRouteValues(context.HttpContext.GetRouteData().Values);

        if (context.TryGetKey(out var key))
        {
            var cacheValue = await Cache.GetAsync<object?>(key);
            if (cacheValue is not null)
            {
                context.AddCacheStatus(CacheStatus.Hit);
                return cacheValue;
            }
            else
            {
                var result = await next(context);
                if (result.IsSuccessfulEndpointResult())
                {
                    context.AddCacheStatus(CacheStatus.Add);
                    await Cache.SetAsync(key, result);
                }
                else
                {
                    context.AddCacheStatus(CacheStatus.None);
                    LogResultNotCacheable();
                }

                return result;
            }
        }
        else
        {
            context.AddCacheStatus(CacheStatus.Miss);
            LogCacheKeyUnavailable();
            return await next(context);
        }
    }
}
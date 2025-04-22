using ActionCache.Common.Enums;
using ActionCache.Common.Extensions;
using ActionCache.Common.Extensions.Internal;
using ActionCache.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;

namespace ActionCache.EndpointFilters;

/// <summary>
/// An endpoint filter that evicts a cached response if the HTTP response is successful.
/// </summary>
public class ActionCacheEndpointEvictionFilter : ActionCacheFilterBase, IEndpointFilter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCacheEndpointEvictionFilter"/> class.
    /// </summary>
    /// <param name="cache">The action cache used for storing or evicting cached responses.</param>
    /// <param name="binderFactory">The template binder factory used for route value extraction.</param>
    public ActionCacheEndpointEvictionFilter(
        IActionCache cache, 
        TemplateBinderFactory binderFactory
    ) : base(cache, binderFactory)
    {
    }

    /// <summary>
    /// Executes the filter logic to evict cached data if the response is successful.
    /// </summary>
    /// <param name="context">The <see cref="EndpointFilterInvocationContext"/> for the current request.</param>
    /// <param name="next">The delegate to invoke the next filter or endpoint in the pipeline.</param>
    /// <returns>The result of the executed filter or endpoint.</returns>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);
        
        if (context.HttpContext.Response.IsSuccessStatusCode())
        {
            AttachRouteValues(context.HttpContext.GetRouteData().Values);
            context.HttpContext.Response.Headers.AddCacheStatus(CacheStatus.Evict);

            await Cache.RemoveAsync();
        }

        return result;
    }
}
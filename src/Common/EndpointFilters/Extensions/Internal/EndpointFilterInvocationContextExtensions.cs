using ActionCache.Common.Enums;
using ActionCache.Common.Extensions.Internal;
using ActionCache.Common.Keys;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ActionCache.MinimalApi.Extensions.Internal;

/// <summary>
/// Provides extensions for <see cref="EndpointFilterInvocationContext"/>.
/// </summary>
internal static class EndpointFilterInvocationContextExtensions
{
    /// <summary>
    /// Tries to generate a cache key from the given context.
    /// </summary>
    /// <param name="context">The endpoint filter invocation context containing necessary data.</param>
    /// <param name="key">Outputs the generated cache key.</param>
    /// <returns>True if a key is successfully generated, otherwise false.</returns>
    internal static bool TryGetKey(this EndpointFilterInvocationContext context, out string key) 
    {
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint is null)
        {
            key = default!;
            return false;
        }
        else
        {
            var parameters = endpoint.RequestDelegate?.Method.GetParameters() ?? [];
            var actionArguments = parameters?
                .Zip(context.Arguments, (parameter, argument) => (parameter.Name, argument))?
                .ToDictionary();

            key = new ActionCacheKeyBuilder()
                .WithRouteValues(context.HttpContext.GetRouteData().Values)
                .WithActionArguments(actionArguments)
                .Build();

            if (string.IsNullOrWhiteSpace(key))
            {
                key = string.Empty;
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Adds the specified cache status to the HTTP response headers of the given action executing context.
    /// </summary>
    /// <param name="context">
    /// The <see cref="EndpointFilterInvocationContext"/> containing the HTTP context where the cache status will be added.
    /// </param>
    /// <param name="status">
    /// The <see cref="CacheStatus"/> to include in the response headers.
    /// </param>
    internal static void AddCacheStatus(
        this EndpointFilterInvocationContext context, 
        CacheStatus status
    ) => context.HttpContext.Response.Headers.AddCacheStatus(status);
}
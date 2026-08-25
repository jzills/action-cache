using ActionCache.Common.Enums;
using Microsoft.AspNetCore.Http;

namespace ActionCache.Filters;

/// <summary>
/// A filter factory attribute that creates an <see cref="ActionCache.EndpointFilters.ActionCacheEndpointRefreshFilter"/> to refresh cached entries for the configured namespace.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ActionCacheEndpointRefreshFilterFactory : ActionCacheEndpointFilterFactoryBase
{
    /// <summary>
    /// Creates an instance of the endpoint cache refresh filter using the specified service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve dependencies.</param>
    /// <returns>An instance of an endpoint cache refresh filter.</returns>
    public override IEndpointFilter CreateInstance(IServiceProvider serviceProvider) =>
        CreateInstance(serviceProvider, FilterType.Refresh);
}

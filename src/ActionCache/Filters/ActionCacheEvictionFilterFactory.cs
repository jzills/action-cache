using ActionCache.Common.Enums;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ActionCache.Filters;

/// <summary>
/// Provides a factory for creating instances of <see cref="ActionCacheEvictionFilter"/> based on specified namespaces.
/// </summary>
public class ActionCacheEvictionFilterFactory : ActionCacheFilterFactoryBase
{
    /// <summary>
    /// Creates an instance of an action cache eviction filter using the provided service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider to use for getting action cache services.</param>
    /// <returns>An instance of <see cref="IFilterMetadata"/> representing the action cache eviction filter.</returns>
    public override IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        CreateInstance(serviceProvider, FilterType.Evict);
}
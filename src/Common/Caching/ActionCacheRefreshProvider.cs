using ActionCache.Common.Extensions.Internal;
using ActionCache.Common.Keys;
using ActionCache.Utilities;

namespace ActionCache.Common.Caching;

/// <summary>
/// Re-invokes cached controller actions to produce up-to-date values for each cache key.
/// </summary>
public class ActionCacheRefreshProvider : IActionCacheRefreshProvider
{
    /// <summary>
    /// Provides access to action cache descriptors, which are used to manage cache-related metadata for actions.
    /// </summary>
    protected readonly IActionCacheDescriptorProvider DescriptorProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCacheRefreshProvider"/> class with the specified descriptor provider.
    /// </summary>
    /// <param name="descriptorProvider">
    /// The <see cref="IActionCacheDescriptorProvider"/> instance used to retrieve cache descriptors for refreshing cached actions.
    /// </param>
    public ActionCacheRefreshProvider(IActionCacheDescriptorProvider descriptorProvider)
    {
        DescriptorProvider = descriptorProvider;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> GetRefreshResults(Namespace @namespace, IEnumerable<string> keys)
    {
        var refreshResults = new Dictionary<string, object?>();
        var descriptorCollection = DescriptorProvider.GetControllerActionMethodInfo(@namespace);
        if (descriptorCollection.MethodInfos.Any())
        {
            if (keys.Some())
            {
                foreach (var key in keys)
                {
                    // Recreate the key components from the encrypted key value
                    var keyComponents = new ActionCacheKeyComponentsBuilder(key).Build();

                    // Deconstruct the route values used as a key into the methodInfo
                    // for a given controller action
                    var (areaName, controllerName, actionName) = keyComponents;
                    var routeValuesKey = DescriptorProvider.CreateKey(areaName, controllerName, actionName);

                    if (descriptorCollection.MethodInfos.TryGetValue(
                            routeValuesKey, 
                            out var methodInfo
                        ))
                    {
                        if (descriptorCollection.Controllers.TryGetValue(routeValuesKey, out var controller))
                        {
                            if (methodInfo.TryGetRefreshResult(
                                    controller, 
                                    keyComponents.ActionArguments?.Values?.ToArray(), 
                                    out var value
                            ))
                            {
                                refreshResults.Add(key, value);
                            }
                        }
                    }
                }
            }
        }

        return refreshResults;
    }
}
using Microsoft.AspNetCore.Routing;

namespace ActionCache.Common.Keys;

/// <summary>
/// Provides functionality to build cache keys for action methods.
/// </summary>
public class ActionCacheKeyBuilder
{
    /// <summary>
    /// The key separator used to delineate between key components.
    /// </summary>
    protected static readonly char KeySeparator = ':';

    /// <summary>
    /// The key generator, by default, implements AES encryption to allow symmetric operations.
    /// </summary> 
    protected readonly KeyEncoder KeyEncoder = new();

    /// <summary>
    /// A key component derived from the route data and action arguments associated with an incoming request. 
    /// </summary>
    protected readonly ActionCacheKeyComponents KeyComponents = new();

    /// <summary>
    /// Includes route values in the cache key.
    /// </summary>
    /// <param name="routeValues">Route values for the action.</param>
    /// <returns>Returns itself for chaining.</returns>
    public ActionCacheKeyBuilder WithRouteValues(RouteValueDictionary routeValues)
    {
        KeyComponents.RouteValues = routeValues;
        return this;
    }

    /// <summary>
    /// Includes action arguments in the cache key.
    /// </summary>
    /// <param name="actionArguments">Arguments for the action.</param>
    /// <returns>Returns itself for chaining.</returns>
    public ActionCacheKeyBuilder WithActionArguments(IDictionary<string, object?> actionArguments)
    {
        if (actionArguments is null) return this;
        KeyComponents.ActionArguments = actionArguments.ToDictionary();
        return this;
    }

    /// <summary>
    /// Builds the final cache key.
    /// </summary>
    /// <returns>The constructed cache key.</returns>
    public string Build() => KeyEncoder.Encode(KeyComponents.Serialize());
}
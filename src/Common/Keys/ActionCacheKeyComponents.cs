using ActionCache.Common.Extensions.Internal;
using ActionCache.Common.Serialization;
using ActionCache.Utilities;
using Microsoft.AspNetCore.Routing;

namespace ActionCache.Common.Keys;

/// <summary>
/// Represents the components used to generate a unique cache key for an action in an HTTP request.
/// Contains route values and action arguments that can uniquely identify a request.
/// </summary>
public class ActionCacheKeyComponents
{
    /// <summary>
    /// The key used to reference route values in the cache.
    /// </summary>
    public const string RouteValuesKey = nameof(RouteValuesKey);

    /// <summary>
    /// The key used to reference action arguments in the cache.
    /// </summary>
    public const string ActionArgumentsKey = nameof(ActionArgumentsKey);

    /// <summary>
    /// Gets or sets the route values used to uniquely identify the route for caching purposes.
    /// </summary>
    public RouteValueDictionary? RouteValues { get; set; } = new();

    /// <summary>
    /// Gets or sets the action arguments used as additional context for identifying and caching the action.
    /// </summary>
    public Dictionary<string, object?>? ActionArguments { get; set; } = new Dictionary<string, object?>();
    
    /// <summary>
    /// Serializes route values and action arguments as key value pairs.
    /// </summary>
    /// <returns>A serialized string representing the key components.</returns>
    public string Serialize()
    {
        var routeValues = CacheJsonSerializer.Serialize(RouteValues);
        var actionArguments = CacheJsonSerializer.Serialize(ActionArguments);
        return $"{RouteValuesKey}={routeValues}&{ActionArgumentsKey}={actionArguments}";
    }

    /// <summary>
    /// Deconstructs the route values into the area, controller, and action name components.
    /// </summary>
    /// <param name="area">The area name, or <see langword="null"/> if not present in the route values.</param>
    /// <param name="controller">The controller name, or <see langword="null"/> if not present in the route values.</param>
    /// <param name="action">The action name, or <see langword="null"/> if not present in the route values.</param>
    public void Deconstruct(out string? area, out string? controller, out string? action)
    {
        ArgumentNullException.ThrowIfNull(RouteValues);

        RouteValues.TryGetStringValue(RouteKeys.Area, out area);
        RouteValues.TryGetStringValue(RouteKeys.Controller, out controller);
        RouteValues.TryGetStringValue(RouteKeys.Action, out action);
    }
}
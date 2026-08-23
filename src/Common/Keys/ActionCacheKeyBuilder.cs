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
    /// Encodes key components into a reversible, non-confidential representation.
    /// This is hex encoding, NOT encryption: cache keys embed the serialized route
    /// values and action arguments in cleartext and can be decoded by anyone with
    /// read access to the cache store. Do not place secrets in route values or
    /// action arguments, and secure the cache store accordingly.
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
    public ActionCacheKeyBuilder WithActionArguments(IDictionary<string, object?>? actionArguments)
    {
        if (actionArguments is null) return this;
        KeyComponents.ActionArguments = actionArguments.ToDictionary();
        return this;
    }

    /// <summary>
    /// Includes vary-by values in the cache key, separating responses that differ by
    /// caller, header, query value, claim, or a registered key contributor.
    /// </summary>
    /// <param name="varyByValues">The resolved vary-by values, or <see langword="null"/>.</param>
    /// <returns>Returns itself for chaining.</returns>
    public ActionCacheKeyBuilder WithVaryByValues(SortedDictionary<string, string?>? varyByValues)
    {
        if (varyByValues is null or { Count: 0 }) return this;
        KeyComponents.VaryByValues = varyByValues;
        return this;
    }

    /// <summary>
    /// Builds the final cache key.
    /// </summary>
    /// <returns>The constructed cache key.</returns>
    public string Build() => KeyEncoder.Encode(KeyComponents.Serialize());
}
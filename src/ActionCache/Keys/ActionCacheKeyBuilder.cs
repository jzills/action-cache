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
    /// Encodes key components reversibly, used only when plaintext keys are enabled.
    /// </summary>
    protected readonly KeyEncoder KeyEncoder = new();

    /// <summary>
    /// Controls how the key is formed.
    /// </summary>
    private readonly ActionCacheKeyOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCacheKeyBuilder"/> class.
    /// </summary>
    /// <param name="options">
    /// How to form the key, or <see langword="null"/> for the hashed default.
    /// </param>
    public ActionCacheKeyBuilder(ActionCacheKeyOptions? options = null) =>
        _options = options ?? new ActionCacheKeyOptions();

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
    /// <remarks>
    /// Hashed by default: the components include route values and action arguments, and a
    /// reversible key hands every one of them to anyone who can read the cache. Nothing
    /// needs to reverse a key any more — refresh replays the request recorded on the entry
    /// itself — so hashing costs nothing but inspectability, which
    /// <see cref="ActionCacheKeyOptions.UsePlaintextKeys"/> restores when debugging.
    /// </remarks>
    public string Build()
    {
        var components = KeyComponents.Serialize();

        return _options.UsePlaintextKeys
            ? KeyEncoder.Encode(components)
            : KeyHashGenerator.ToHash(components);
    }
}
namespace ActionCache.Utilities;

/// <summary>
/// Defines constants for common route keys used in MVC routing, such as controller, action, and area names.
/// </summary>
internal static class RouteKeys
{
    /// <summary>
    /// Represents the key for specifying the controller name in routing.
    /// </summary>
    internal const string Controller = nameof(Controller);

    /// <summary>
    /// Represents the key for specifying the action name in routing.
    /// </summary>
    internal const string Action = nameof(Action);

    /// <summary>
    /// Represents the key for specifying the area name in routing.
    /// </summary>
    internal const string Area = nameof(Area);
}
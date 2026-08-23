using ActionCache.Filters;

namespace ActionCache.Attributes;

/// <summary>
/// Specifies caching behavior for an action method.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ActionCacheAttribute : ActionCacheFilterFactory
{
}
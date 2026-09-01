using ActionCache.Filters;

namespace ActionCache.Attributes;

/// <summary>
/// Provides an attribute to specify refresh behavior for cached action results.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ActionCacheRefreshAttribute : ActionCacheRefreshFilterFactory
{
}
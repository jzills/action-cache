using ActionCache.Filters;

namespace ActionCache.Attributes;

/// <summary>
/// Represents an attribute that can be used to specify that the cache should be evicted.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ActionCacheEvictionAttribute : ActionCacheEvictionFilterFactory
{
}
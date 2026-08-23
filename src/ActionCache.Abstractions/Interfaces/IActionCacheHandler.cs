namespace ActionCache.Common.Caching;

/// <summary>
/// Defines functionality for managing a chain of action cache handlers.
/// </summary>
public interface IActionCacheHandler
{
    /// <summary>
    /// Sets the next action cache in the chain of responsibility.
    /// </summary>
    /// <param name="next">The next <see cref="IActionCache"/> to process in the chain.</param>
    /// <returns>The current <see cref="IActionCache"/> handler for method chaining.</returns>
    IActionCache SetNext(IActionCache next);
}
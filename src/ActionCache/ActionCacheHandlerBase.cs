namespace ActionCache.Common.Caching;

/// <summary>
/// Provides a base implementation for handling action cache operations with support for chaining multiple caches.
/// </summary>
public class ActionCacheHandlerBase : IActionCacheHandler
{
    /// <summary>
    /// Gets or sets the next <see cref="IActionCache"/> instance in the chain.
    /// This allows for chaining multiple action cache handlers together.
    /// </summary>
    protected IActionCache? Next { get; set; } 

    /// <summary>
    /// Sets the next <see cref="IActionCache"/> instance in the chain and returns it.
    /// This method enables chaining of cache handlers by passing the next cache instance.
    /// </summary>
    /// <param name="next">The next <see cref="IActionCache"/> instance in the chain.</param>
    /// <returns>The <see cref="IActionCache"/> instance provided in the <paramref name="next"/> parameter.</returns>
    public IActionCache SetNext(IActionCache next)
    {
        Next = next;
        return next;
    }

    /// <summary>
    /// A flag indicating if a next handler is available.
    /// </summary>
    public bool IsNextAvailable => Next is not null;

    /// <summary>
    /// Executes an asynchronous action on the next cache in the chain, if it exists.
    /// </summary>
    /// <param name="action">The action to execute on the next cache.</param>
    /// <returns>A task that represents the asynchronous action.</returns>
    protected async Task NextIfExists(Func<IActionCache, Task> action)
    {
        if (IsNextAvailable)
        {
#pragma warning disable CS8604

            await action(Next);

#pragma warning restore CS8604
        }
    }

    /// <summary>
    /// Executes an asynchronous action on the next cache in the chain, if it exists, and returns the result.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the action.</typeparam>
    /// <param name="action">The action to execute on the next cache.</param>
    /// <returns>The result of the action, or <c>null</c> if the next cache does not exist.</returns>
    protected async Task<T?> NextIfExists<T>(Func<IActionCache, Task<T>> action)
    {
        if (IsNextAvailable)
        {
#pragma warning disable CS8604

            return await action(Next);

#pragma warning restore CS8604
        }
        else
        {
            return default;
        }
    }
}
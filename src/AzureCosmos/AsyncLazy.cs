namespace ActionCache.AzureCosmos;

/// <summary>
/// A lazily-initialized asynchronous value. The <c>factory</c> runs at
/// most once (on first access to <see cref="Lazy{T}.Value"/>); every awaiter observes
/// the same underlying <see cref="Task{TResult}"/>.
/// </summary>
/// <typeparam name="T">The type of value produced.</typeparam>
public class AsyncLazy<T> : Lazy<Task<T>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class.
    /// </summary>
    /// <param name="factory">The asynchronous factory that produces the value on first access.</param>
    public AsyncLazy(Func<Task<T>> factory)
        : base(() => Task.Run(factory))
    {
    }
}

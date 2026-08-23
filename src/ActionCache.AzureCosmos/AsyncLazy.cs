namespace ActionCache.AzureCosmos;

/// <summary>
/// A lazily-initialized asynchronous value. The <c>factory</c> runs on first access to
/// <see cref="Value"/> and its result is cached; concurrent awaiters share the same
/// in-flight <see cref="Task{TResult}"/>. If the factory's task faults or is cancelled,
/// the failure is NOT cached — the next access re-runs the factory — so a transient
/// initialization failure does not permanently disable the value for the process lifetime.
/// </summary>
/// <typeparam name="T">The type of value produced.</typeparam>
public class AsyncLazy<T>
{
    private readonly Func<Task<T>> _factory;
    private readonly object _gate = new();
    private Task<T>? _task;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class.
    /// </summary>
    /// <param name="factory">The asynchronous factory that produces the value on first access.</param>
    public AsyncLazy(Func<Task<T>> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Gets the task producing the value, starting the factory on first access and
    /// re-starting it if the previous attempt faulted or was cancelled.
    /// </summary>
    public Task<T> Value
    {
        get
        {
            lock (_gate)
            {
                if (_task is null || _task.IsFaulted || _task.IsCanceled)
                {
                    _task = Task.Run(_factory);
                }

                return _task;
            }
        }
    }
}

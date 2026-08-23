namespace ActionCache.Common.Concurrency;

/// <summary>
/// Represents a mechanism for acquiring and releasing distributed locks then handling some action in a caching system.
/// </summary>
public interface ICacheLockerHandler
{
    /// <summary>
    /// Asynchronously waits for a lock to be acquired, then executes an action if the lock is acquired.
    /// </summary>
    /// <param name="resource">The resource for which the lock is requested.</param>
    /// <param name="thenFunc">The action to be executed after the lock is acquired.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task WaitForLockThenAsync(string resource, Action thenFunc);

    /// <summary>
    /// Asynchronously waits for a lock to be acquired, then executes a function that returns a result if the lock is acquired.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the function.</typeparam>
    /// <param name="resource">The resource for which the lock is requested.</param>
    /// <param name="resultAccessor">The function that will return a result once the lock is acquired.</param>
    /// <returns>A task that represents the asynchronous operation, containing the result of the function or default value if lock is not acquired.</returns>
    Task<TResult?> WaitForLockThenAsync<TResult>(string resource, Func<TResult> resultAccessor);

    /// <summary>
    /// Waits for the lock to be acquired on the specified resource, then executes the provided action.
    /// </summary>
    /// <param name="resource">The resource to acquire the lock for.</param>
    /// <param name="thenFunc">The action to execute once the lock is acquired.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the lock cannot be acquired within the configured timeout.</exception>
    Task WaitForLockThenAsync(string resource, Func<Task> thenFunc);

    /// <summary>
    /// Waits for the lock on the specified resource, runs the provided function under it, and
    /// reports whether the lock was acquired — without throwing when it was not.
    /// </summary>
    /// <typeparam name="TResult">The type of the result produced by the function.</typeparam>
    /// <param name="resource">The resource to acquire the lock for.</param>
    /// <param name="resultAccessor">The function executed once the lock is held.</param>
    /// <returns>
    /// The function's result together with whether the lock was acquired. When it was not,
    /// the function did not run.
    /// </returns>
    /// <remarks>
    /// Prefer this over the throwing overloads wherever a busy lock is an expected outcome
    /// rather than an error. Exceptions raised by <paramref name="resultAccessor"/> propagate
    /// unchanged, so a caller cannot mistake a failure of the work for a failure to lock.
    /// </remarks>
    Task<CacheLockAttempt<TResult>> TryWaitForLockThenAsync<TResult>(string resource, Func<Task<TResult>> resultAccessor);

    /// <summary>
    /// Waits for the lock to be acquired on the specified resource, then executes the provided function and returns the result.
    /// </summary>
    /// <typeparam name="TResult">The type of the result produced by the function.</typeparam>
    /// <param name="resource">The resource to acquire the lock for.</param>
    /// <param name="resultAccessor">The function that will be executed once the lock is acquired, which returns a result.</param>
    /// <returns>A task representing the asynchronous operation, with the result of the function.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the lock cannot be acquired within the configured timeout.</exception>
    Task<TResult?> WaitForLockThenAsync<TResult>(string resource, Func<Task<TResult>> resultAccessor);
}
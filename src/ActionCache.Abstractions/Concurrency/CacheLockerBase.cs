namespace ActionCache.Common.Concurrency;

/// <summary>
/// Base class for cache lockers, responsible for acquiring and releasing locks on cache resources.
/// </summary>
/// <typeparam name="TLock">The type of the cache lock used for this locker.</typeparam>
public abstract class CacheLockerBase<TLock> : ICacheLocker<TLock> where TLock : CacheLock
{
    /// <summary>
    /// How long acquisition waits before giving up.
    /// </summary>
    /// <remarks>
    /// The only timing every locker can honour. A <em>lease</em> — a deadline after which a
    /// held lock is considered abandoned — is not here, because only a backend whose locks
    /// carry a time-to-live can impose one. <c>RedisCacheLocker</c> takes its lease as its
    /// own constructor parameter; the others hold until released, and a process that dies
    /// releases its locks by exiting.
    /// </remarks>
    protected readonly TimeSpan LockTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheLockerBase{T}"/> class.
    /// </summary>
    /// <param name="lockTimeout">How long acquisition waits before giving up.</param>
    public CacheLockerBase(TimeSpan lockTimeout)
    {
        LockTimeout = lockTimeout;
    }

    /// <summary>
    /// Releases the specified cache lock.
    /// </summary>
    /// <param name="cacheLock">The cache lock to release.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public abstract Task ReleaseLockAsync(TLock cacheLock);

    /// <summary>
    /// Attempts to acquire a lock for the specified resource.
    /// </summary>
    /// <param name="resource">The resource to acquire the lock for.</param>
    /// <returns>A task that represents the asynchronous operation, with the acquired cache lock.</returns>
    public abstract Task<TLock> TryAcquireLockAsync(string resource);

    /// <summary>
    /// Waits for the lock on the specified resource to be acquired and returns the lock.
    /// </summary>
    /// <param name="resource">The resource to acquire the lock for.</param>
    /// <returns>A task that represents the asynchronous operation, with the acquired cache lock.</returns>
    public abstract Task<TLock> WaitForLockAsync(string resource);

    /// <summary>
    /// Asynchronously waits for a lock to be acquired, then executes an action if the lock is acquired.
    /// </summary>
    /// <param name="resource">The resource for which the lock is requested.</param>
    /// <param name="thenFunc">The action to be executed after the lock is acquired.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task WaitForLockThenAsync(string resource, Action thenFunc) =>
        WaitForLockThenAsync(resource, () => {
            thenFunc();
            return Task.CompletedTask;
        });

    /// <summary>
    /// Asynchronously waits for a lock to be acquired, then executes a function that returns a result if the lock is acquired.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the function.</typeparam>
    /// <param name="resource">The resource for which the lock is requested.</param>
    /// <param name="resultAccessor">The function that will return a result once the lock is acquired.</param>
    /// <returns>A task that represents the asynchronous operation, containing the result of the function or default value if lock is not acquired.</returns>
    public virtual Task<TResult?> WaitForLockThenAsync<TResult>(string resource, Func<TResult> resultAccessor) =>
        WaitForLockThenAsync(resource, () => Task.FromResult(resultAccessor()));

    /// <summary>
    /// Waits for the lock to be acquired on the specified resource, then executes the provided action.
    /// </summary>
    /// <param name="resource">The resource to acquire the lock for.</param>
    /// <param name="thenFunc">The action to execute once the lock is acquired.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the lock cannot be acquired within the configured timeout.</exception>
    public virtual async Task WaitForLockThenAsync(string resource, Func<Task> thenFunc)
    {
        var cacheLock = await WaitForLockAsync(resource);
        if (cacheLock.IsAcquired)
        {
            try
            {
                await thenFunc();
            }
            finally
            {
                await ReleaseLockAsync(cacheLock);
            }
        }
        else
        {
            throw new InvalidOperationException($"Failed to acquire lock for resource '{resource}' within the configured timeout.");
        }
    }

    /// <inheritdoc/>
    public virtual async Task<CacheLockAttempt<TResult>> TryWaitForLockThenAsync<TResult>(
        string resource,
        Func<Task<TResult>> resultAccessor)
    {
        var cacheLock = await WaitForLockAsync(resource);
        if (!cacheLock.IsAcquired)
        {
            return new CacheLockAttempt<TResult>(LockAcquired: false, Result: default);
        }

        try
        {
            return new CacheLockAttempt<TResult>(LockAcquired: true, Result: await resultAccessor());
        }
        finally
        {
            await ReleaseLockAsync(cacheLock);
        }
    }

    /// <summary>
    /// Waits for the lock to be acquired on the specified resource, then executes the provided function and returns the result.
    /// </summary>
    /// <typeparam name="TResult">The type of the result produced by the function.</typeparam>
    /// <param name="resource">The resource to acquire the lock for.</param>
    /// <param name="resultAccessor">The function that will be executed once the lock is acquired, which returns a result.</param>
    /// <returns>A task representing the asynchronous operation, with the result of the function.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the lock cannot be acquired within the configured timeout.</exception>
    public virtual async Task<TResult?> WaitForLockThenAsync<TResult>(string resource, Func<Task<TResult>> resultAccessor)
    {
        TResult? result = default;

        var cacheLock = await WaitForLockAsync(resource);
        if (cacheLock.IsAcquired)
        {
            try
            {
                result = await resultAccessor();
            }
            finally
            {
                await ReleaseLockAsync(cacheLock);
            }
        }
        else
        {
            throw new InvalidOperationException($"Failed to acquire lock for resource '{resource}' within the configured timeout.");
        }

        return result;
    }
}
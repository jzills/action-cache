using System.Collections.Concurrent;
using ActionCache.Common.Concurrency.Locks;

namespace ActionCache.Common.Concurrency;

/// <summary>
/// An in-process keyed async mutex. Each distinct resource gets its own binary
/// <see cref="SemaphoreSlim"/>, ref-counted so that resources seen once — for example the
/// per-id namespaces produced by a route-templated namespace such as <c>Account:{id}</c> —
/// do not accumulate for the lifetime of the process.
/// </summary>
/// <remarks>
/// A semaphore has no time-to-live, so this locker takes no lease: a lock is held until
/// released, and the <c>finally</c> in every caller is what guarantees that. A process that
/// dies releases its locks by exiting.
/// </remarks>
/// <remarks>
/// Instances must be shared to be meaningful: caches are constructed per request, so a
/// locker created inside a cache factory's <c>Create</c> would guard nothing. Register as
/// a singleton.
/// </remarks>
public class SemaphoreSlimCacheLocker : CacheLockerBase<SemaphoreSlimLock>
{
    private readonly ConcurrentDictionary<string, SemaphoreSlimLockEntry> _entries = new();
    private readonly object _gate = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SemaphoreSlimCacheLocker"/> class.
    /// </summary>
    /// <param name="lockTimeout">The maximum time <see cref="WaitForLockAsync"/> waits before giving up.</param>
    public SemaphoreSlimCacheLocker(TimeSpan lockTimeout)
        : base(lockTimeout)
    {
    }

    /// <summary>
    /// The number of resources currently tracked. Zero when every acquired lock has been released.
    /// </summary>
    internal int TrackedResourceCount => _entries.Count;

    /// <inheritdoc/>
    public override Task<SemaphoreSlimLock> TryAcquireLockAsync(string resource) =>
        AcquireAsync(resource, TimeSpan.Zero);

    /// <inheritdoc/>
    public override Task<SemaphoreSlimLock> WaitForLockAsync(string resource) =>
        AcquireAsync(resource, LockTimeout);

    /// <inheritdoc/>
    public override Task ReleaseLockAsync(SemaphoreSlimLock cacheLock)
    {
        // Guard against double release: a second call must not hand out an extra permit.
        if (!cacheLock.IsAcquired || cacheLock.Entry is null)
        {
            return Task.CompletedTask;
        }

        var entry = cacheLock.Entry;
        cacheLock.IsAcquired = false;
        cacheLock.Entry = null;

        entry.Semaphore.Release();
        Return(cacheLock.Resource, entry);

        return Task.CompletedTask;
    }

    private async Task<SemaphoreSlimLock> AcquireAsync(string resource, TimeSpan timeout)
    {
        var cacheLock = new SemaphoreSlimLock(resource, LockTimeout);
        var entry = Rent(resource);

        // The semaphore is never disposed, so waiting on it can never race a disposal.
        if (await entry.Semaphore.WaitAsync(timeout))
        {
            cacheLock.IsAcquired = true;
            cacheLock.Entry = entry;
        }
        else
        {
            Return(resource, entry);
        }

        return cacheLock;
    }

    private SemaphoreSlimLockEntry Rent(string resource)
    {
        // Renting and returning share one gate so an entry cannot be removed between a
        // caller finding it and incrementing its ref count.
        lock (_gate)
        {
            var entry = _entries.GetOrAdd(resource, _ => new SemaphoreSlimLockEntry());
            entry.RefCount++;
            return entry;
        }
    }

    private void Return(string resource, SemaphoreSlimLockEntry entry)
    {
        lock (_gate)
        {
            if (--entry.RefCount <= 0)
            {
                _entries.TryRemove(resource, out _);
            }
        }
    }
}

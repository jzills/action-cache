using ActionCache.Common.Diagnostics;
using ActionCache.Utilities;
using Microsoft.Extensions.Logging;

namespace ActionCache.Common.Concurrency;

/// <summary>
/// Coalesces concurrent misses within a single process.
/// </summary>
/// <remarks>
/// Owns a private <see cref="SemaphoreSlimCacheLocker"/> rather than sharing the one the
/// memory backend uses for its key index. The two nest — this lock is held across the value
/// factory, which writes to the cache and takes the index lock — and separate lockers make a
/// key collision between the two structurally impossible. The ordering is one-way: nothing
/// may take the index lock and then a single-flight lock.
/// </remarks>
public class InProcessSingleFlight : IActionCacheSingleFlight
{
    private readonly SemaphoreSlimCacheLocker _locker;
    private readonly ILogger<InProcessSingleFlight> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InProcessSingleFlight"/> class.
    /// </summary>
    /// <param name="options">Supplies the lease duration and how long a caller waits for it.</param>
    /// <param name="logger">Records coalescing and lock-timeout outcomes.</param>
    public InProcessSingleFlight(
        ActionCacheSingleFlightOptions options,
        ILogger<InProcessSingleFlight> logger
    )
    {
        // Waits for the single-flight timeout, not the key-index lock's: this lock is held
        // across the origin action, the index lock across a dictionary update. A semaphore
        // cannot expire, so LeaseDuration has nothing to enforce here — it exists for the
        // distributed lockers that can.
        _locker = new SemaphoreSlimCacheLocker(options.WaitTimeout);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SingleFlightResult<TValue>> GetOrCreateAsync<TValue>(
        Namespace @namespace,
        string key,
        Func<Task<TValue?>> cacheReader,
        Func<Task<TValue?>> valueFactory
    )
    {
        var resource = $"{(string)@namespace}:{key}";
        var cacheLock = await _locker.WaitForLockAsync(resource);

        if (!cacheLock.IsAcquired)
        {
            ActionCacheLog.SingleFlightLockTimeout(_logger, key, @namespace);
            return new SingleFlightResult<TValue>(await valueFactory(), WasCoalesced: false);
        }

        try
        {
            var cached = await cacheReader();
            if (cached is not null)
            {
                ActionCacheLog.SingleFlightCoalesced(_logger, key, @namespace);
                ActionCacheDiagnostics.SingleFlightCoalesced.Add(1,
                    new KeyValuePair<string, object?>("namespace", (string)@namespace));
                return new SingleFlightResult<TValue>(cached, WasCoalesced: true);
            }

            return new SingleFlightResult<TValue>(await valueFactory(), WasCoalesced: false);
        }
        finally
        {
            await _locker.ReleaseLockAsync(cacheLock);
        }
    }
}

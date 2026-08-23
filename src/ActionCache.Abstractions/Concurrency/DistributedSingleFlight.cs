using ActionCache.Common.Diagnostics;
using ActionCache.Utilities;
using Microsoft.Extensions.Logging;

namespace ActionCache.Common.Concurrency;

/// <summary>
/// Coalesces concurrent misses across every instance of the application, using a backend's
/// distributed lock so that exactly one node executes the origin action.
/// </summary>
/// <remarks>
/// Every miss costs a lock round-trip to the backend, which is why the in-process
/// implementation remains the default. Uses the same <c>{namespace}:{key}</c> resource
/// format as <see cref="InProcessSingleFlight"/> so both lock the same logical resource.
/// </remarks>
public class DistributedSingleFlight : IActionCacheSingleFlight
{
    private readonly ICacheLockerHandler _locker;
    private readonly ILogger<DistributedSingleFlight> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedSingleFlight"/> class.
    /// </summary>
    /// <param name="locker">The distributed locker used to elect a leader.</param>
    /// <param name="logger">Records coalescing and lock-timeout outcomes.</param>
    public DistributedSingleFlight(
        ICacheLockerHandler locker,
        ILogger<DistributedSingleFlight> logger
    )
    {
        _locker = locker;
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

        // TryWaitForLockThenAsync reports a busy lock in its result rather than by throwing.
        // The previous version inferred the timeout from an InvalidOperationException, which
        // the origin action can raise just as easily as the locker: that logged a misleading
        // lock timeout and then ran the action a second time.
        var attempt = await _locker.TryWaitForLockThenAsync(resource, async () =>
        {
            var cached = await cacheReader();
            if (cached is not null)
            {
                ActionCacheLog.SingleFlightCoalesced(_logger, key, @namespace);
                ActionCacheDiagnostics.SingleFlightCoalesced.Add(1,
                    new KeyValuePair<string, object?>("namespace", @namespace.Value));
                return new SingleFlightResult<TValue>(cached, WasCoalesced: true);
            }

            return new SingleFlightResult<TValue>(await valueFactory(), WasCoalesced: false);
        });

        if (attempt.LockAcquired)
        {
            return attempt.Result;
        }

        ActionCacheLog.SingleFlightLockTimeout(_logger, key, @namespace);
        return new SingleFlightResult<TValue>(await valueFactory(), WasCoalesced: false);
    }
}

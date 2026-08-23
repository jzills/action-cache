using Microsoft.Extensions.Logging;

namespace ActionCache.Common.Diagnostics;

/// <summary>
/// Source-generated log messages for ActionCache. Centralizing every message here keeps
/// call sites to a single line and keeps <see cref="EventId"/> values collision-free.
/// </summary>
/// <remarks>
/// EventId ranges: 1xxx cache operations (<c>ResilientActionCache</c>), 2xxx filter-level
/// conditions the cache layer cannot observe, 3xxx refresh provider, 4xxx factory
/// cache-creation failures, 5xxx Redis expiry subscription retries, 6xxx single-flight, 7xxx vary-by.
/// Hit/miss/set/evict/refresh outcomes are logged only by the 1xxx events; filters do not
/// duplicate them.
/// </remarks>
internal static partial class ActionCacheLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Debug, Message = "ActionCache hit for key '{Key}' in namespace '{Namespace}'.")]
    public static partial void CacheHit(ILogger logger, string key, string @namespace);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "ActionCache miss for key '{Key}' in namespace '{Namespace}'.")]
    public static partial void CacheMiss(ILogger logger, string key, string @namespace);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Debug, Message = "ActionCache set key '{Key}' in namespace '{Namespace}'.")]
    public static partial void CacheSet(ILogger logger, string key, string @namespace);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Debug, Message = "ActionCache removed key '{Key}' in namespace '{Namespace}'.")]
    public static partial void CacheKeyRemoved(ILogger logger, string key, string @namespace);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Debug, Message = "ActionCache evicted namespace '{Namespace}'.")]
    public static partial void CacheEvicted(ILogger logger, string @namespace);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Debug, Message = "ActionCache refreshed namespace '{Namespace}'.")]
    public static partial void CacheRefreshed(ILogger logger, string @namespace);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Warning, Message = "ActionCache backend operation '{Operation}' failed for namespace '{Namespace}'; degrading gracefully.")]
    public static partial void OperationDegraded(ILogger logger, Exception exception, string operation, string @namespace);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Error, Message = "ActionCache backend operation '{Operation}' failed for namespace '{Namespace}'; propagating (fail-closed).")]
    public static partial void OperationFailedClosed(ILogger logger, Exception exception, string operation, string @namespace);

    [LoggerMessage(EventId = 2000, Level = LogLevel.Debug, Message = "{Filter} could not construct a cache key for namespace '{Namespace}'; the request executed uncached.")]
    public static partial void FilterCacheKeyUnavailable(ILogger logger, string filter, string @namespace);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Debug, Message = "{Filter} did not cache the response for namespace '{Namespace}' because the result was not a cacheable success result.")]
    public static partial void FilterResultNotCacheable(ILogger logger, string filter, string @namespace);

    [LoggerMessage(EventId = 3000, Level = LogLevel.Debug, Message = "ActionCache refresh skipped key '{Key}' in namespace '{Namespace}': {Reason}.")]
    public static partial void RefreshKeySkipped(ILogger logger, string key, string @namespace, string reason);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "ActionCache refresh for namespace '{Namespace}' refreshed {RefreshedCount} of {RequestedCount} keys.")]
    public static partial void RefreshSummary(ILogger logger, string @namespace, int refreshedCount, int requestedCount);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Warning, Message = "ActionCache refresh for namespace '{Namespace}' found no matching controller actions; {RequestedCount} requested keys were not refreshed.")]
    public static partial void RefreshNoActionsFound(ILogger logger, string @namespace, int requestedCount);

    [LoggerMessage(EventId = 4000, Level = LogLevel.Warning, Message = "{Factory} failed to create an ActionCache instance for namespace '{Namespace}'.")]
    public static partial void CacheCreationFailed(ILogger logger, string factory, string @namespace);

    [LoggerMessage(EventId = 5000, Level = LogLevel.Warning, Message = "ActionCache could not subscribe to Redis keyspace expiry notifications on database {Database}; retrying in {RetryDelay}. Until then, sliding-expiration index cleanup relies on lazy self-healing.")]
    public static partial void RedisExpirySubscriptionFailed(ILogger logger, Exception exception, int database, TimeSpan retryDelay);

    [LoggerMessage(EventId = 6000, Level = LogLevel.Debug, Message = "ActionCache single-flight coalesced a waiter for key '{Key}' in namespace '{Namespace}'.")]
    public static partial void SingleFlightCoalesced(ILogger logger, string key, string @namespace);

    [LoggerMessage(EventId = 6001, Level = LogLevel.Debug, Message = "ActionCache single-flight could not acquire the lock for key '{Key}' in namespace '{Namespace}' within the timeout; executing uncoalesced.")]
    public static partial void SingleFlightLockTimeout(ILogger logger, string key, string @namespace);

    [LoggerMessage(EventId = 7000, Level = LogLevel.Debug, Message = "ActionCache vary-by contributed {Count} value(s) to the cache key.")]
    public static partial void VaryByResolved(ILogger logger, int count);

    [LoggerMessage(EventId = 7001, Level = LogLevel.Warning, Message = "ActionCache refresh skipped key '{Key}' in namespace '{Namespace}' because it varies by request context that refresh cannot reproduce.")]
    public static partial void RefreshKeySkippedVaryBy(ILogger logger, string key, string @namespace);
}

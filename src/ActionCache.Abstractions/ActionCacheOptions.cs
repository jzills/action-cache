using ActionCache.Common.Concurrency;
using Microsoft.Extensions.DependencyInjection;

namespace ActionCache.Common;

/// <summary>
/// Provides configuration options for action caching.
/// </summary>
public class ActionCacheOptions
{
    /// <summary>
    /// Gets the default entry options for the cache.
    /// </summary>
    public readonly ActionCacheEntryOptions EntryOptions = new();

    /// <summary>
    /// Gets or sets a value indicating whether cache-backend failures propagate to
    /// the caller (fail-closed). Defaults to <see langword="false"/> (fail-open).
    /// </summary>
    public bool FailClosed { get; set; }

    /// <summary>
    /// Gets or sets the maximum time a single cache-backend operation may take before it is
    /// abandoned. <see langword="null"/> (default) imposes no timeout.
    /// </summary>
    public TimeSpan? OperationTimeout { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether single-flight coalescing is coordinated
    /// across every instance of the application using a backend's distributed lock,
    /// rather than only within one process. Defaults to <see langword="false"/>.
    /// </summary>
    public bool UseDistributedSingleFlight { get; set; }

    /// <summary>
    /// Gets the options controlling how concurrent misses for one key are coalesced.
    /// </summary>
    public ActionCacheSingleFlightOptions SingleFlightOptions { get; } = new();

    /// <summary>
    /// Whether cache keys are readable and reversible rather than hashed.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool UsePlaintextKeys { get; set; }

    /// <summary>
    /// Registrations contributed by backend packages.
    /// </summary>
    /// <remarks>
    /// The core package deliberately does not know which backends exist. Each backend
    /// package contributes its own registration through its <c>Use…Cache</c> extension, so
    /// referencing ActionCache alone pulls in no Redis, SQL Server or Cosmos dependency.
    /// </remarks>
    internal List<Action<IServiceCollection>> BackendRegistrations { get; } = [];

    /// <summary>
    /// Builds the distributed locker used by <c>UseDistributedSingleFlight()</c>, set by a
    /// backend package that supports one.
    /// </summary>
    internal Func<IServiceProvider, ICacheLockerHandler>? DistributedLockerFactory { get; set; }
}

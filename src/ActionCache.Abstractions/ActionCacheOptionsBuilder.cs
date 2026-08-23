using ActionCache.Common.Concurrency;
using Microsoft.Extensions.DependencyInjection;

namespace ActionCache.Common;

/// <summary>
/// Provides a builder for configuring ActionCache options.
/// </summary>
public class ActionCacheOptionsBuilder
{
    /// <summary>
    /// Stores the options for the ActionCache.
    /// </summary>
    protected readonly ActionCacheOptions Options = new();

    /// <summary>
    /// Configures the entry options for the action cache.
    /// </summary>
    /// <param name="configureOptions">The delegate to configure the entry options.</param>
    /// <returns>Returns this instance of <see cref="ActionCacheOptionsBuilder"/>.</returns>
    public ActionCacheOptionsBuilder UseEntryOptions(Action<ActionCacheEntryOptions> configureOptions)
    {
        configureOptions.Invoke(Options.EntryOptions);
        return this;
    }






    /// <summary>
    /// Configures ActionCache to fail closed: cache-backend failures propagate to the
    /// caller instead of being swallowed. By default ActionCache fails open.
    /// </summary>
    /// <param name="failClosed">
    /// <see langword="true"/> (default) to fail closed; <see langword="false"/> to fail open.
    /// </param>
    /// <returns>Returns this instance of <see cref="ActionCacheOptionsBuilder"/>.</returns>
    public ActionCacheOptionsBuilder FailClosed(bool failClosed = true)
    {
        Options.FailClosed = failClosed;
        return this;
    }

    /// <summary>
    /// Bounds how long a single cache-backend operation may take before it is abandoned.
    /// </summary>
    /// <param name="timeout">The maximum duration of one backend operation.</param>
    /// <returns>Returns this instance of <see cref="ActionCacheOptionsBuilder"/>.</returns>
    /// <remarks>
    /// Fail-open catches exceptions; it does not bound a backend that hangs rather than
    /// throws. Without a timeout such a backend hangs the request indefinitely.
    /// </remarks>
    public ActionCacheOptionsBuilder UseOperationTimeout(TimeSpan timeout)
    {
        Options.OperationTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Coordinates single-flight across every instance of the application using the
    /// configured Redis or SQL Server backend's distributed lock, instead of coalescing
    /// only within one process.
    /// </summary>
    /// <returns>Returns this instance of <see cref="ActionCacheOptionsBuilder"/>.</returns>
    /// <remarks>
    /// Every cache miss then costs a lock round-trip to the backend. Requires Redis or SQL
    /// Server to be configured; Redis is preferred when both are.
    /// </remarks>
    public ActionCacheOptionsBuilder UseDistributedSingleFlight(
        Action<ActionCacheSingleFlightOptions>? configureOptions = null)
    {
        Options.UseDistributedSingleFlight = true;
        configureOptions?.Invoke(Options.SingleFlightOptions);
        return this;
    }

    /// <summary>
    /// Configures how concurrent misses for one cache key are coalesced.
    /// </summary>
    /// <param name="configureOptions">The delegate to configure the single-flight options.</param>
    /// <returns>Returns this instance of <see cref="ActionCacheOptionsBuilder"/>.</returns>
    /// <remarks>
    /// Applies to both the in-process default and <see cref="UseDistributedSingleFlight"/>.
    /// Raise <see cref="ActionCacheSingleFlightOptions.LeaseDuration"/> above the slowest
    /// action the cache fronts.
    /// </remarks>
    public ActionCacheOptionsBuilder UseSingleFlightOptions(
        Action<ActionCacheSingleFlightOptions> configureOptions)
    {
        configureOptions.Invoke(Options.SingleFlightOptions);
        return this;
    }

    /// <summary>
    /// Registers a backend's services. Called by a backend package's <c>Use…Cache</c>
    /// extension, so the core package never has to know which backends exist.
    /// </summary>
    /// <param name="register">Adds the backend's services to the container.</param>
    /// <returns>Returns this instance of <see cref="ActionCacheOptionsBuilder"/>.</returns>
    public ActionCacheOptionsBuilder AddBackend(Action<IServiceCollection> register)
    {
        Options.BackendRegistrations.Add(register);
        return this;
    }

    /// <summary>
    /// Supplies the distributed locker that <see cref="UseDistributedSingleFlight"/> uses.
    /// Called by a backend package that offers distributed locking.
    /// </summary>
    /// <param name="lockerFactory">Builds the locker from the application's services.</param>
    /// <returns>Returns this instance of <see cref="ActionCacheOptionsBuilder"/>.</returns>
    public ActionCacheOptionsBuilder AddDistributedLocker(
        Func<IServiceProvider, ICacheLockerHandler> lockerFactory)
    {
        Options.DistributedLockerFactory = lockerFactory;
        return this;
    }

    /// <summary>
    /// Builds the configured <see cref="ActionCacheOptions"/>.
    /// </summary>
    /// <returns>The configured <see cref="ActionCacheOptions"/>.</returns>
    internal ActionCacheOptions Build() => Options;
}
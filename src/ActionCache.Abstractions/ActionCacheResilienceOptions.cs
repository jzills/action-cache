namespace ActionCache.Common.Caching;

/// <summary>
/// Configures how ActionCache reacts to cache-backend failures.
/// </summary>
public class ActionCacheResilienceOptions
{
    /// <summary>
    /// When <see langword="false"/> (default), backend failures are logged and
    /// swallowed so the request still succeeds without caching (fail-open). When
    /// <see langword="true"/>, backend failures propagate to the caller (fail-closed).
    /// </summary>
    public bool FailClosed { get; set; }

    /// <summary>
    /// The maximum time a single cache-backend operation may take before it is abandoned.
    /// <see langword="null"/> (default) imposes no timeout.
    /// </summary>
    /// <remarks>
    /// Fail-open catches exceptions; it does not bound a backend that hangs rather than
    /// throws. This does. An elapsed timeout is treated as a backend failure — degraded
    /// under fail-open, rethrown under fail-closed — and is distinct from the caller's own
    /// cancellation, which always propagates.
    /// </remarks>
    public TimeSpan? OperationTimeout { get; set; }
}

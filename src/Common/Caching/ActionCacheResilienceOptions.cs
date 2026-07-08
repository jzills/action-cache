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
}

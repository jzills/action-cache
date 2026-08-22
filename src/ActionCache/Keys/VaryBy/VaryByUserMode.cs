namespace ActionCache.Common.Keys.VaryBy;

/// <summary>
/// Controls whether the authenticated user's identity forms part of the cache key.
/// </summary>
public enum VaryByUserMode
{
    /// <summary>
    /// Vary by user when the request is authenticated, and not otherwise. This is the
    /// default: an endpoint that returns per-user data gets per-user cache entries without
    /// anyone having to remember to ask, which is what prevents one user's response from
    /// being served to another.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Always vary by user. Anonymous requests share a single anonymous bucket.
    /// </summary>
    Always = 1,

    /// <summary>
    /// Never vary by user: one shared entry across all callers. Correct only when the
    /// response genuinely does not depend on who asked for it.
    /// </summary>
    Never = 2
}

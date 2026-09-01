namespace ActionCache.Common.Keys;

/// <summary>
/// Controls how cache keys are formed.
/// </summary>
public class ActionCacheKeyOptions
{
    /// <summary>
    /// Whether keys embed route values and action arguments in a readable, reversible form
    /// instead of being hashed. Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Plaintext keys make cache contents easy to inspect, which is genuinely useful while
    /// debugging. They also mean anyone who can read the cache can recover every route
    /// value and action argument that produced an entry — ids, filters, search terms.
    /// Hashing is the default for that reason.
    /// </remarks>
    public bool UsePlaintextKeys { get; set; }
}

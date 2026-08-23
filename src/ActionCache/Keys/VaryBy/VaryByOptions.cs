namespace ActionCache.Common.Keys.VaryBy;

/// <summary>
/// The vary-by settings declared on a caching attribute, describing which request
/// dimensions form part of the cache key.
/// </summary>
public class VaryByOptions
{
    /// <summary>
    /// Whether the authenticated user's identity forms part of the key.
    /// Defaults to <see cref="VaryByUserMode.Auto"/>.
    /// </summary>
    public VaryByUserMode User { get; set; } = VaryByUserMode.Auto;

    /// <summary>
    /// A comma-separated list of request header names to vary by, or <see langword="null"/>.
    /// </summary>
    public string? Headers { get; set; }

    /// <summary>
    /// A comma-separated list of query-string keys to vary by, or <see langword="null"/>.
    /// </summary>
    public string? Query { get; set; }

    /// <summary>
    /// A comma-separated list of claim types to vary by, or <see langword="null"/>.
    /// </summary>
    public string? Claims { get; set; }
}

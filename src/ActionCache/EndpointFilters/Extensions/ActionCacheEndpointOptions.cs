using ActionCache.Common.Keys.VaryBy;

namespace ActionCache.EndpointFilters.Extensions;

/// <summary>
/// The per-endpoint caching settings a Minimal API endpoint can declare, matching what
/// <c>[ActionCache]</c> exposes on a controller action.
/// </summary>
/// <remarks>
/// Expirations are <see cref="TimeSpan"/> rather than the milliseconds the attribute takes.
/// That is not a gratuitous difference: an attribute argument must be a compile-time
/// constant, so the attribute cannot hold a <see cref="TimeSpan"/> and states its
/// expirations as <see cref="long"/> instead. A builder has no such constraint.
/// </remarks>
public sealed class ActionCacheEndpointOptions
{
    /// <summary>
    /// How long an entry lives from the moment it is written, or <see langword="null"/> to
    /// leave it to whatever <c>UseEntryOptions</c> configures globally.
    /// </summary>
    public TimeSpan? AbsoluteExpiration { get; set; }

    /// <summary>
    /// How long an entry lives after it was last read, or <see langword="null"/> to leave it
    /// to whatever <c>UseEntryOptions</c> configures globally.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    /// Whether the authenticated user's identity forms part of the cache key.
    /// </summary>
    /// <value>
    /// Defaults to <see cref="VaryByUserMode.Auto"/>: authenticated requests get per-user
    /// cache entries automatically, which is what stops one user's response being served to
    /// another. Set to <see cref="VaryByUserMode.Never"/> for a response that genuinely does
    /// not depend on who asked.
    /// </value>
    public VaryByUserMode VaryByUser { get; set; } = VaryByUserMode.Auto;

    /// <summary>
    /// A comma-separated list of request header names to vary the cache key by.
    /// </summary>
    public string? VaryByHeader { get; set; }

    /// <summary>
    /// A comma-separated list of query-string keys to vary the cache key by.
    /// </summary>
    /// <remarks>
    /// A Minimal API handler that binds a query parameter already has it in the key as an
    /// argument. This is for a query string the handler does not bind but the response still
    /// depends on.
    /// </remarks>
    public string? VaryByQuery { get; set; }

    /// <summary>
    /// A comma-separated list of claim types to vary the cache key by.
    /// </summary>
    public string? VaryByClaim { get; set; }

    /// <summary>
    /// Whether concurrent misses for one cache key are coalesced so the endpoint executes once.
    /// </summary>
    /// <value>
    /// Defaults to <see langword="true"/>. Set to <see langword="false"/> to let every
    /// concurrent miss execute the endpoint, which is only appropriate when it has
    /// per-request side effects that must not be skipped.
    /// </value>
    public bool SingleFlight { get; set; } = true;
}

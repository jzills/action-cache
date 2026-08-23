namespace ActionCache.Common.Responses;

/// <summary>
/// A cached HTTP response, stored as primitives only.
/// </summary>
/// <remarks>
/// Deliberately non-polymorphic. Earlier versions serialized the
/// <see cref="Microsoft.AspNetCore.Mvc.IActionResult"/> graph itself with type names
/// embedded, which meant deserialization had to resolve arbitrary types from the cache
/// and be defended with a binder. Storing a rendered response instead removes that
/// vulnerability class rather than filtering it, and keeps the payload portable across
/// every backend.
/// </remarks>
public sealed record CachedResponse
{
    /// <summary>
    /// The HTTP status code of the cached response.
    /// </summary>
    public required int StatusCode { get; init; }

    /// <summary>
    /// The response content type, or <see langword="null"/> when the response had no body.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// The already-serialized response body, or <see langword="null"/> when there was none.
    /// </summary>
    public string? Body { get; init; }

    /// <summary>
    /// Whether the key this entry is stored under varies by request context — the
    /// authenticated user, a header, a query value, a claim, or a key contributor.
    /// </summary>
    /// <remarks>
    /// Refresh uses this to skip entries it cannot faithfully reproduce: replaying another
    /// caller's request would mean impersonating them.
    /// </remarks>
    public bool VariesByRequest { get; init; }

    /// <summary>
    /// The request that produced this entry, recorded so refresh can replay it.
    /// </summary>
    public CachedRequest? Request { get; init; }
}

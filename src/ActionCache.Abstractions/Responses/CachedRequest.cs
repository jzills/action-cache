namespace ActionCache.Common.Responses;

/// <summary>
/// The request line that produced a cached response, recorded so that refresh can
/// re-issue it.
/// </summary>
/// <remarks>
/// <para>
/// The method, path, query string and — for requests that had one — the body, which is
/// what a cached <c>[FromBody]</c> action needs in order to be replayed at all. Before
/// responses were stored as envelopes, that payload lived in the cache key itself, in
/// reversible cleartext; keeping it here instead is strictly less exposed, and keys are
/// now hashed.
/// </para>
/// <para>
/// Headers remain deliberately excluded: they routinely carry credentials, and unlike the
/// body they are not needed to reproduce the response.
/// </para>
/// </remarks>
public sealed record CachedRequest
{
    /// <summary>
    /// The HTTP method of the recorded request.
    /// </summary>
    public required string Method { get; init; }

    /// <summary>
    /// The path of the recorded request.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The query string of the recorded request, including the leading '?', or
    /// <see langword="null"/> when there was none.
    /// </summary>
    public string? QueryString { get; init; }

    /// <summary>
    /// The request body, or <see langword="null"/> when there was none.
    /// </summary>
    /// <remarks>
    /// Re-serialized from the bound model rather than captured as raw bytes, so it may
    /// differ from the original byte-for-byte (property order, casing) while binding to an
    /// equivalent model — which is what a replay needs, and what keeps the refreshed value
    /// landing on the same cache key.
    /// </remarks>
    public string? Body { get; init; }

    /// <summary>
    /// The content type to replay the body with, or <see langword="null"/> when there is no body.
    /// </summary>
    public string? ContentType { get; init; }
}

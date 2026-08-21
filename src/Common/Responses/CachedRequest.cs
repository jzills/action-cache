namespace ActionCache.Common.Responses;

/// <summary>
/// The request line that produced a cached response, recorded so that refresh can
/// re-issue it.
/// </summary>
/// <remarks>
/// Only the method, path and query string are kept. Headers are deliberately excluded:
/// they routinely carry credentials, and a cache entry is not a safe place for them.
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
}

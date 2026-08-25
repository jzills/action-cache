namespace ActionCache.Exceptions;

/// <summary>
/// Thrown at startup when the cache attributes on one or more endpoints cannot coexist.
/// </summary>
/// <remarks>
/// Reported when the host starts rather than when a request arrives, because every conflict
/// this detects is one an application would otherwise survive: the endpoint responds normally
/// and the caching, eviction or refresh silently does not happen.
/// </remarks>
public class ConflictingCacheAttributesException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConflictingCacheAttributesException"/>
    /// class describing every endpoint that failed validation.
    /// </summary>
    /// <param name="conflicts">One description per offending endpoint.</param>
    public ConflictingCacheAttributesException(IReadOnlyList<string> conflicts)
        : base(Format(conflicts))
    {
    }

    /// <summary>
    /// Renders the conflicts as a single message, one endpoint per line.
    /// </summary>
    /// <param name="conflicts">One description per offending endpoint.</param>
    /// <returns>The exception message.</returns>
    private static string Format(IReadOnlyList<string> conflicts) =>
        $"ActionCache found {(conflicts.Count == 1 ? "an endpoint whose" : $"{conflicts.Count} endpoints whose")} " +
        $"cache attributes conflict:{Environment.NewLine}{Environment.NewLine}" +
        string.Join(Environment.NewLine, conflicts.Select(conflict => $"  - {conflict}"));
}

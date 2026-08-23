using Microsoft.AspNetCore.Http;

namespace ActionCache.Common.Keys.VaryBy;

/// <summary>
/// Contributes additional values to a cache key, for dimensions the built-in vary-by
/// properties cannot express — a tenant read from a subdomain, a feature-flag cohort,
/// an API version negotiated per request.
/// </summary>
/// <remarks>
/// Every registered contributor runs for every cached request. Values land in a sorted
/// collection, so the order contributors run in does not affect the resulting key.
/// </remarks>
public interface IActionCacheKeyContributor
{
    /// <summary>
    /// Adds this contributor's values to the cache key.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="varyByValues">The vary-by values collected so far; add to it.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask ContributeAsync(
        HttpContext httpContext,
        IDictionary<string, string?> varyByValues,
        CancellationToken cancellationToken);
}

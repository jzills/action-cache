using ActionCache.Utilities;

namespace ActionCache.Common.Concurrency;

/// <summary>
/// A no-op <see cref="IActionCacheSingleFlight"/> that always runs the value factory without
/// coalescing. Used by the filters that never produce cache entries — eviction and refresh —
/// so they need not carry a single-flight dependency they would never call.
/// </summary>
internal sealed class NullActionCacheSingleFlight : IActionCacheSingleFlight
{
    /// <summary>
    /// The shared instance.
    /// </summary>
    internal static readonly NullActionCacheSingleFlight Instance = new();

    private NullActionCacheSingleFlight()
    {
    }

    /// <inheritdoc/>
    public async Task<SingleFlightResult<TValue>> GetOrCreateAsync<TValue>(
        Namespace @namespace,
        string key,
        Func<Task<TValue?>> cacheReader,
        Func<Task<TValue?>> valueFactory
    ) => new(await valueFactory(), WasCoalesced: false);
}

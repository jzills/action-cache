namespace ActionCache.Memory;

/// <summary>
/// Defines a mechanism for retrieving or creating <see cref="CancellationTokenSource"/> instances keyed by a cache namespace.
/// </summary>
public interface IExpirationTokenSources
{
    /// <summary>
    /// Retrieves an existing <see cref="CancellationTokenSource"/> for the given key, or creates and stores a new one if none exists.
    /// </summary>
    /// <param name="key">The cache key used to look up or register the token source.</param>
    /// <param name="cancellationTokenSource">When this method returns, contains the <see cref="CancellationTokenSource"/> associated with <paramref name="key"/>.</param>
    /// <returns><see langword="true"/> if the token source was successfully retrieved or created; otherwise, <see langword="false"/>.</returns>
    bool TryGetOrAdd(string key, out CancellationTokenSource cancellationTokenSource);
}
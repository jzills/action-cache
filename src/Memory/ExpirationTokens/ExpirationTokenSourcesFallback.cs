using System.Collections.Concurrent;

namespace ActionCache.Memory;

/// <summary>
/// Represents a collection of expiration token sources with a fallback mechanism.
/// </summary>
public class ExpirationTokenSourcesFallback : IExpirationTokenSources
{
    /// <summary>
    /// Thread-safe dictionary mapping cache keys to their associated <see cref="CancellationTokenSource"/> instances.
    /// </summary>
    protected ConcurrentDictionary<string, CancellationTokenSource> Tokens = new();

    /// <summary>
    /// Tries to get an existing cancellationTokenSource for a given key, or adds a new one if not found.
    /// </summary>
    /// <param name="key">The key to look up in the dictionary.</param>
    /// <param name="cancellationTokenSource">The cancellationTokenSource associated with the key.</param>
    /// <returns>True if the operation was successful, False otherwise.</returns>
    public bool TryGetOrAdd(string key, out CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            cancellationTokenSource = Tokens.GetOrAdd(key, new CancellationTokenSource());

            // If the token has been cancelled, then remove it and add a new cancellationTokenSource for future cache entries
            if (cancellationTokenSource.IsCancellationRequested)
            {
                if (Tokens.TryRemove(key, out _))
                {
                    cancellationTokenSource = Tokens.GetOrAdd(key, new CancellationTokenSource());
                }
            }

            return true;
        }
        catch (Exception)
        {
            cancellationTokenSource = default!;
            return false;
        }
    }

    /// <inheritdoc/>
    public void Reset(string key)
    {
        if (Tokens.TryRemove(key, out var cancellationTokenSource))
        {
            cancellationTokenSource.Cancel();
        }
    }
}

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace ActionCache.Memory;

/// <summary>
/// Manages <see cref="CancellationTokenSource"/> instances used as expiration tokens for memory cache entries.
/// </summary>
public class ExpirationTokenSources : IExpirationTokenSources
{
    /// <summary>
    /// The cache used for storing expiration token sources.
    /// </summary>
    protected readonly IMemoryCache Cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpirationTokenSources"/> class.
    /// </summary>
    /// <param name="cache">The memory cache used to store and retrieve <see cref="CancellationTokenSource"/> instances.</param>
    public ExpirationTokenSources(IMemoryCache cache) => Cache = cache;

    /// <summary>
    /// Creates memory cache entry options.
    /// </summary>
    /// <param name="source">The cancellation token source to use in the options.</param>
    /// <returns>The memory cache entry options object.</returns>
    public MemoryCacheEntryOptions EntryOptions(CancellationTokenSource source) => 
        new MemoryCacheEntryOptions { Size = 1 }
            .AddExpirationToken(
                new CancellationChangeToken(source.Token));

    /// <summary>
    /// Tries to get the value associated with the key from the cache. If it's not found, adds it using the provided cancellation token source.
    /// </summary>
    /// <param name="key">The key to search for in the cache.</param>
    /// <param name="cancellationTokenSource">The cancellation token source associated with the key.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public bool TryGetOrAdd(string key, out CancellationTokenSource cancellationTokenSource)
    {
        if (!Cache.TryGetValue(key, out cancellationTokenSource!))
        {
            cancellationTokenSource ??= new CancellationTokenSource();
            Cache.Set(key, cancellationTokenSource, EntryOptions(cancellationTokenSource));
        }
        
        return true;
    }
}
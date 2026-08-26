using ActionCache.Utilities;
using ActionCache.Common;

namespace ActionCache;

/// <summary>
/// Provides an interface for caching actions in a key-value store.
/// </summary>
public interface IActionCache
{
    /// <summary>
    /// Retrieves the namespace associated with the specified cache.
    /// </summary>
    /// <returns>The namespace for this cache.</returns>
    Namespace GetNamespace();

    /// <summary>
    /// Retrieves the keys associated with the specified namespace from the cache.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The keys associated with the specified cache namespace.</returns>
    Task<IEnumerable<string>> GetKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the value associated with the specified key from the cache.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to retrieve.</typeparam>
    /// <param name="key">The key of the value to retrieve.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The value associated with the specified key, or null if the key does not exist.</returns>
    Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the value with the specified key in the cache.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to set.</typeparam>
    /// <param name="key">The key of the value to store.</param>
    /// <param name="value">The value to store. Can be null.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A task that represents the asynchronous set operation.</returns>
    Task SetAsync<TValue>(string key, TValue? value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a value under the specified key, expiring it by the given options rather than
    /// the ones this cache was created with.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to store.</typeparam>
    /// <param name="key">The key of the value to store.</param>
    /// <param name="value">The value to store. Can be null.</param>
    /// <param name="entryOptions">
    /// The expirations to write this entry with, or <see langword="null"/> to use the cache's own.
    /// </param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A task that represents the asynchronous set operation.</returns>
    /// <remarks>
    /// Exists for refresh. A refresh filter is created without expirations, so writing a
    /// replayed response through the cache's own options replaced whatever the cached endpoint
    /// declared with the global defaults — one refresh was enough to make a time-limited entry
    /// permanent. A namespace can hold entries from several endpoints with different
    /// expirations, so the correct value is per entry rather than per cache.
    /// </remarks>
    Task SetAsync<TValue>(string key, TValue? value, ActionCacheEntryOptions? entryOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the value to remove.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A task that represents the asynchronous remove operation.</returns>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all values and keys from the cache.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A task that represents the asynchronous remove operation.</returns>
    Task RemoveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshs all values and keys from the cache.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A task that represents the asynchronous refresh operation.</returns>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
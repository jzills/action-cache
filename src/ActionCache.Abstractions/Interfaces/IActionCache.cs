using ActionCache.Utilities;

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
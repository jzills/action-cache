using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.Azure.Cosmos;
using ActionCache.Common;
using ActionCache.Common.Caching;
using ActionCache.Common.Serialization;
using ActionCache.Utilities;
using ActionCache.AzureCosmos.Extensions;
using ActionCache.Common.Concurrency.Locks;

namespace ActionCache.AzureCosmos;

/// <summary>
/// Represents an Azure Cosmos DB action cache implementation.
/// </summary>
public class AzureCosmosActionCache : ActionCacheBase<NullCacheLock>
{
    /// <summary>
    /// The lazily-initialized Azure Cosmos DB cache container.
    /// </summary>
    protected readonly AsyncLazy<Container> Cache;

    /// <summary>
    /// The namespaced partition key.
    /// </summary>
    protected readonly PartitionKey PartitionKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureCosmosActionCache"/> class.
    /// </summary>
    /// <param name="cache">The lazily-initialized Azure Cosmos DB container instance.</param>
    /// <param name="context">The cache context.</param>
    public AzureCosmosActionCache(AsyncLazy<Container> cache, ActionCacheContext<NullCacheLock> context) : base(context)
    {
        Cache = cache;
        PartitionKey = new PartitionKey(Namespace);
    }

    /// <summary>
    /// Asynchronously gets a value from the cache.
    /// </summary>
    /// <param name="key">The key of the cache entry.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The cached value or null if not found.</returns>
#pragma warning disable CS8609
    public override async Task<TValue> GetAsync<TValue>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var container = await Cache.Value;
        try
        {
            var response = await container.ReadItemAsync<AzureCosmosEntry>(
                Namespace.Create(key),
                PartitionKey,
                requestOptions: null,
                cancellationToken
            );

            if (ActionCacheEntryOptions.HasExpiredAbsoluteExpiration(response.Resource.AbsoluteExpiration))
            {
                await container.DeleteItemAsync<AzureCosmosEntry>(
                    Namespace.Create(key),
                    PartitionKey,
                    requestOptions: null,
                    cancellationToken
                );

                return default!;
            }

            if (ActionCacheEntryOptions.HasSlidingExpiration(response.Resource.SlidingExpiration))
            {
                await container.ReplaceItemAsync(
                    response.Resource,
                    response.Resource.Id,
                    PartitionKey
                );
            }

            return CacheJsonSerializer.Deserialize<TValue>(response.Resource.Value)!;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Silence errors for entries not found on a particular key.
            return default!;
        }
    }
#pragma warning restore CS8609

    /// <summary>
    /// Asynchronously sets a value in the cache.
    /// </summary>
    /// <param name="key">The cache key to set the value for.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    public override async Task SetAsync<TValue>(string key, [AllowNull] TValue value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var container = await Cache.Value;
        var (absoluteExpiration, slidingExpiration, ttl) = EntryOptions;

        await container.UpsertItemAsync(new AzureCosmosEntry
        {
            Id = Namespace.Create(key),
            Key = key,
            Namespace = Namespace,
            Value = CacheJsonSerializer.Serialize(value),
            AbsoluteExpiration = absoluteExpiration,
            SlidingExpiration = slidingExpiration,
            TTL = ttl == ActionCacheEntryOptions.NoExpiration ? -1 : (long)Math.Ceiling(ttl / 1000.0)
        }, PartitionKey, requestOptions: null, cancellationToken);
    }

    /// <summary>
    /// Asynchronously removes a value from the cache.
    /// </summary>
    /// <param name="key">The key of the cache entry to remove.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    public override async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var container = await Cache.Value;
        try
        {
            await container.DeleteItemAsync<AzureCosmosEntry>(
                Namespace.Create(key),
                PartitionKey,
                requestOptions: null,
                cancellationToken
            );
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Silence errors for entries not found on a particular key.
        }
    }

    /// <summary>
    /// Asynchronously removes all values from the cache.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    public override async Task RemoveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var container = await Cache.Value;
        var response = await container.DeleteAllItemsByPartitionKeyStreamAsync(PartitionKey);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var items = await container.GetItemsAsync(Namespace);
            if (items.Any())
            {
                await Task.WhenAll(items.Select(item => RemoveAsync(item.Key)));
            }
        }
    }

    /// <summary>
    /// Retrieves all keys associated with this cache.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>An enumerable of strings representing current cache entry keys.</returns>
    public override async Task<IEnumerable<string>> GetKeysAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var container = await Cache.Value;
        var items = await container.GetItemsAsync(Namespace);
        if (items.Any())
        {
            var itemsKeys = new List<string>(items.Count);
            var itemsToExpire = new List<Task>(items.Count);
            foreach (var item in items)
            {
                if (ActionCacheEntryOptions.HasExpiredAbsoluteExpiration(item.AbsoluteExpiration))
                {
                    itemsToExpire.Add(RemoveAsync(item.Key));
                }
                else
                {
                    itemsKeys.Add(item.Id);
                }
            }

            await Task.WhenAll(itemsToExpire);

            return itemsKeys;
        }
        else
        {
            return [];
        }
    }
}

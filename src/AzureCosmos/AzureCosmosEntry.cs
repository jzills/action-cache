using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace ActionCache.AzureCosmos;

/// <summary>
/// Represents an entry in Azure Cosmos.
/// </summary>
public class AzureCosmosEntry
{
    /// <summary>
    /// Gets or sets the unique identifier for the entry.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty(PropertyName = "id")] 
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the key for the entry.
    /// </summary>
    [JsonPropertyName("key")]
    [JsonProperty(PropertyName = "key")] 
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets the namespace associated with the entry.
    /// </summary>
    [JsonPropertyName("namespace")]
    [JsonProperty(PropertyName = "namespace")] 
    public required string Namespace { get; set; }

    /// <summary>
    /// Gets or sets the value associated with the entry.
    /// </summary>
    [JsonPropertyName("value")]
    [JsonProperty(PropertyName = "value")]  
    public required string Value { get; set; }

    /// <summary>
    /// Gets or sets the absolute expiration of the entry as a Unix timestamp in milliseconds.
    /// </summary>
    [JsonPropertyName("absoluteExpiration")]
    [JsonProperty(PropertyName = "absoluteExpiration")]
    public long AbsoluteExpiration { get; set; }

    /// <summary>
    /// Gets or sets the sliding expiration of the entry in milliseconds.
    /// </summary>
    [JsonPropertyName("slidingExpiration")]
    [JsonProperty(PropertyName = "slidingExpiration")]
    public long SlidingExpiration { get; set; }

    /// <summary>
    /// Gets or sets the Cosmos DB TTL for the entry in seconds; <c>-1</c> disables TTL-based expiration.
    /// </summary>
    [JsonPropertyName("ttl")]
    [JsonProperty(PropertyName = "ttl")]
    public long TTL { get; set; }
}
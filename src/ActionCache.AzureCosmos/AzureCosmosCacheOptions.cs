using Microsoft.Azure.Cosmos;

namespace ActionCache.AzureCosmos;

/// <summary>
/// Represents configuration options for the Azure Cosmos cache.
/// </summary>
public class AzureCosmosCacheOptions
{
    /// <summary>
    /// Gets or sets the connection string used to connect to the Azure Cosmos DB account.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the options for configuring the Cosmos client.
    /// </summary>
    public CosmosClientOptions? CosmosClientOptions { get; set; }

    /// <summary>
    /// Gets or sets the database identifier for the Azure Cosmos cache.
    /// </summary>
    public string? DatabaseId { get; set; }
}
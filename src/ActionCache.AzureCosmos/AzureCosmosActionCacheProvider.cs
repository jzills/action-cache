using System.Net;
using ActionCache.AzureCosmos.Exceptions;
using ActionCache.Utilities;
using Microsoft.Azure.Cosmos;

namespace ActionCache.AzureCosmos;

/// <summary>
/// Provides functionality for creating and managing the Azure Cosmos DB container
/// used as the ActionCache backing store.
/// </summary>
public class AzureCosmosActionCacheProvider
{
    /// <summary>
    /// The Cosmos DB client used to interact with the Azure Cosmos DB service.
    /// </summary>
    protected readonly CosmosClient CosmosClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureCosmosActionCacheProvider"/> class.
    /// </summary>
    /// <param name="cosmosClient">The Cosmos DB client used for interacting with Azure Cosmos DB.</param>
    public AzureCosmosActionCacheProvider(CosmosClient cosmosClient)
    {
        CosmosClient = cosmosClient;
    }

    /// <summary>
    /// Ensures the database and cache container exist and returns the container.
    /// </summary>
    /// <param name="databaseId">The identifier of the Azure Cosmos DB database.</param>
    /// <returns>A task whose result is the ActionCache <see cref="Container"/>.</returns>
    /// <exception cref="AzureCosmosDatabaseNotFoundOrCreated">
    /// Thrown when the database could not be found or created.
    /// </exception>
    /// <exception cref="AzureCosmosContainerNotFoundOrCreated">
    /// Thrown when the container could not be found or created.
    /// </exception>
    public async Task<Container> CreateContainerAsync(string databaseId)
    {
        var databaseResponse = await CosmosClient
            .CreateDatabaseIfNotExistsAsync(databaseId);

        if (IsSuccessStatusCode(databaseResponse.StatusCode))
        {
            var containerResponse = await databaseResponse.Database
                .CreateContainerIfNotExistsAsync(new ContainerProperties
                {
                    Id = Namespace.Assembly,
                    PartitionKeyPath = "/namespace",
                    DefaultTimeToLive = -1
                });

            if (IsSuccessStatusCode(containerResponse.StatusCode))
            {
                return containerResponse.Container;
            }
            else
            {
                throw new AzureCosmosContainerNotFoundOrCreated(containerResponse);
            }
        }
        else
        {
            throw new AzureCosmosDatabaseNotFoundOrCreated(databaseResponse);
        }
    }

    /// <summary>
    /// Determines whether the provided HTTP status code indicates a successful response.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to evaluate.</param>
    /// <returns><see langword="true"/> if the status code indicates success; otherwise, <see langword="false"/>.</returns>
    private bool IsSuccessStatusCode(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.OK ||
        statusCode == HttpStatusCode.Created;
}

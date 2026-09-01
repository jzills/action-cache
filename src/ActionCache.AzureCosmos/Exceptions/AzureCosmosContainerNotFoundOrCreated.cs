using Microsoft.Azure.Cosmos;

namespace ActionCache.AzureCosmos.Exceptions;

/// <summary>
/// Represents an exception that is thrown when an error occurs
/// fetching or creating an Azure Cosmos container. 
/// </summary>
public class AzureCosmosContainerNotFoundOrCreated : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AzureCosmosContainerNotFoundOrCreated"/> class with an error message containing the HTTP status code from <paramref name="response"/>.
    /// </summary>
    /// <param name="response">The Cosmos DB container response whose status code is included in the exception message.</param>
    public AzureCosmosContainerNotFoundOrCreated(ContainerResponse response)
        : base($"An error occurred fetching or creating Azure Cosmos container ({response.StatusCode}).")
    {
    }
}
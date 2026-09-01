namespace ActionCache.AzureCosmos.Exceptions;

/// <summary>
/// Represents an exception that is thrown when a required database connection string
/// is missing or invalid for "Azure Cosmos Db".
/// </summary>
public class MissingConnectionStringException : ArgumentException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MissingConnectionStringException"/> class
    /// with the default error message.
    /// </summary>
    public MissingConnectionStringException() 
        : base("The connection string cannot be null for \"Azure Cosmos Db\".")
    {
    }
}
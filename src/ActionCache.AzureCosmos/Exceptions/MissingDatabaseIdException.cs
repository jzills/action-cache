namespace ActionCache.AzureCosmos.Exceptions;

/// <summary>
/// Represents an exception that is thrown when a required database identifier
/// is missing or invalid for "Azure Cosmos Db".
/// </summary>
public class MissingDatabaseIdException : ArgumentException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MissingDatabaseIdException"/> class
    /// with the default error message.
    /// </summary>
    public MissingDatabaseIdException() 
        : base("The database identifier cannot be null, empty or whitespace for \"Azure Cosmos Db\".")
    {
    }
}
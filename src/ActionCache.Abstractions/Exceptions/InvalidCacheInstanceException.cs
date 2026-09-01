namespace ActionCache.Exceptions;

/// <summary>
/// Represents an exception that is thrown when a cache instance is invalid or null.
/// </summary>
public class InvalidCacheInstanceException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidCacheInstanceException"/> class 
    /// with a specified error message, or a default message if none is provided.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception. 
    /// If null, a default message is used.</param>
    public InvalidCacheInstanceException(string? message = null)
        : base(message ?? "One or more cache instances are null.")
    {
    }
}
using ActionCache.Common.Enums;

namespace ActionCache.Exceptions;

/// <summary>
/// Represents an exception that is thrown when an unsupported filter type is encountered.
/// </summary>
public class FilterTypeNotSupportedException : NotSupportedException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FilterTypeNotSupportedException"/> class 
    /// with a specified filter type that is not supported.
    /// </summary>
    /// <param name="filterType">The unsupported filter type that caused the exception.</param>
    public FilterTypeNotSupportedException(FilterType filterType)
        : base($"The filter type \"{filterType}\" is not supported.")
    {
    }
}
using Microsoft.AspNetCore.Http;

namespace ActionCache.MinimalApi.Extensions.Internal;

/// <summary>
/// Provides extension methods for evaluating Minimal API endpoint results.
/// </summary>
internal static class EndpointResultExtensions
{
    /// <summary>
    /// Determines whether an endpoint result represents a successful (2xx) response.
    /// A null result is not successful. A result implementing
    /// <see cref="IStatusCodeHttpResult"/> is evaluated against the 200–226 range
    /// (a null status code is treated as 200). Any other non-null value (a raw
    /// object or a status-less result) is treated as a 200 response.
    /// </summary>
    /// <param name="result">The endpoint result to evaluate.</param>
    /// <returns><c>true</c> if the result should be cached; otherwise <c>false</c>.</returns>
    internal static bool IsSuccessfulEndpointResult(this object? result)
    {
        if (result is null)
        {
            return false;
        }

        if (result is IStatusCodeHttpResult statusCodeResult)
        {
            var statusCode = statusCodeResult.StatusCode ?? StatusCodes.Status200OK;
            return statusCode >= StatusCodes.Status200OK &&
                   statusCode <= StatusCodes.Status226IMUsed;
        }

        return true;
    }
}

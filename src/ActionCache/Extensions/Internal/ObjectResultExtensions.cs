using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ActionCache.Common.Extensions.Internal;

/// <summary>
/// Provides extension methods for working with <see cref="ObjectResult"/> objects.
/// </summary>
internal static class ObjectResultExtensions
{
    /// <summary>
    /// Determines whether the status code of the specified <see cref="ObjectResult"/> indicates a successful response.
    /// </summary>
    /// <param name="result">The <see cref="ObjectResult"/> to evaluate.</param>
    /// <returns>
    /// <c>true</c> if the status code is between 200 (OK) and 226 (IM Used), inclusive; otherwise, <c>false</c>.
    /// </returns>
    internal static bool IsSuccessStatusCode(this ObjectResult result)
    {
        var statusCode = result.StatusCode ?? StatusCodes.Status200OK;
        return statusCode >= StatusCodes.Status200OK &&
               statusCode <= StatusCodes.Status226IMUsed;
    }
}
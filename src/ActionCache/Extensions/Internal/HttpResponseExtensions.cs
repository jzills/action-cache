using Microsoft.AspNetCore.Http;

namespace ActionCache.Common.Extensions;

/// <summary>
/// Provides extension methods for <see cref="HttpResponse"/> to check status code success.
/// </summary>
internal static class HttpResponseExtensions
{
    /// <summary>
    /// Determines whether the status code of the specified <see cref="HttpResponse"/> represents a successful status.
    /// </summary>
    /// <param name="response">The <see cref="HttpResponse"/> to evaluate.</param>
    /// <returns>
    /// <c>true</c> if the status code is in the range of successful HTTP status codes (200–226); otherwise, <c>false</c>.
    /// </returns>
    internal static bool IsSuccessStatusCode(this HttpResponse response) =>
        response.StatusCode >= StatusCodes.Status200OK &&
        response.StatusCode <= StatusCodes.Status226IMUsed;
}
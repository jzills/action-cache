using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ActionCache.Common.Extensions.Internal;

/// <summary>
/// Provides extension methods for <see cref="StatusCodeResult"/> to check status code success.
/// </summary>
internal static class StatusCodeResultExtensions
{
    /// <summary>
    /// Determines whether the status code of the specified <see cref="StatusCodeResult"/> represents a successful status.
    /// </summary>
    /// <param name="result">The <see cref="StatusCodeResult"/> to evaluate.</param>
    /// <returns>
    /// <c>true</c> if the status code is in the range of successful HTTP status codes (200–226); otherwise, <c>false</c>.
    /// </returns>
    internal static bool IsSuccessStatusCode(this StatusCodeResult result) =>
        result.StatusCode >= StatusCodes.Status200OK && 
        result.StatusCode <= StatusCodes.Status226IMUsed;
}
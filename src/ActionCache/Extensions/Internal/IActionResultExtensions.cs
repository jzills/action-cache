using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ActionCache.Common.Extensions.Internal;

/// <summary>
/// Provides extension methods for <see cref="IActionResult"/> to evaluate response success based on HTTP status codes.
/// </summary>
internal static class IActionResultExtensions
{
    /// <summary>
    /// Determines whether an <see cref="IActionResult"/> represents a successful result, 
    /// based on it being a successful <see cref="ObjectResult"/> or <see cref="StatusCodeResult"/>.
    /// </summary>
    /// <param name="result">The <see cref="IActionResult"/> to evaluate.</param>
    /// <returns><c>true</c> if the result is successful; otherwise, <c>false</c>.</returns>
    internal static bool IsSuccessfulResult(this IActionResult result) =>
        result.IsSuccessfulObjectResult() || 
        result.IsSuccessfulStatusCodeResult();

    /// <summary>
    /// Determines whether an <see cref="IActionResult"/> is a successful <see cref="ObjectResult"/>, 
    /// with a status code in the successful range (200-299).
    /// </summary>
    /// <param name="result">The <see cref="IActionResult"/> to evaluate.</param>
    /// <returns><c>true</c> if the result is a successful <see cref="ObjectResult"/>; otherwise, <c>false</c>.</returns>
    internal static bool IsSuccessfulObjectResult(this IActionResult result) =>
        result is ObjectResult objectResult && 
            objectResult.IsSuccessStatusCode();

    /// <summary>
    /// Determines whether an <see cref="IActionResult"/> is a successful <see cref="StatusCodeResult"/>, 
    /// with a status code in the successful range (200-299).
    /// </summary>
    /// <param name="result">The <see cref="IActionResult"/> to evaluate.</param>
    /// <returns><c>true</c> if the result is a successful <see cref="StatusCodeResult"/>; otherwise, <c>false</c>.</returns>
    internal static bool IsSuccessfulStatusCodeResult(this IActionResult result) =>
        result is StatusCodeResult statusCodeResult &&
            statusCodeResult.IsSuccessStatusCode();

    /// <summary>
    /// Determines whether an <see cref="IActionResult"/> should be cached: a
    /// result exposing a status code (<see cref="IStatusCodeActionResult"/>) is
    /// cacheable only when that status is in the successful range (200–226, with
    /// a null status treated as 200); any other result carries no status and is
    /// treated as a 200 response, so it is cacheable. Mirrors the Minimal API
    /// <c>IsSuccessfulEndpointResult</c> semantics.
    /// </summary>
    /// <param name="result">The <see cref="IActionResult"/> to evaluate.</param>
    /// <returns><c>true</c> if the result should be cached; otherwise <c>false</c>.</returns>
    internal static bool IsCacheableResult(this IActionResult result)
    {
        if (result is IStatusCodeActionResult statusCodeResult)
        {
            var statusCode = statusCodeResult.StatusCode ?? StatusCodes.Status200OK;
            return statusCode >= StatusCodes.Status200OK &&
                   statusCode <= StatusCodes.Status226IMUsed;
        }

        return true;
    }
}
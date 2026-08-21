using System.Text.Json;
using ActionCache.Common.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ActionCache.Common.Responses;

/// <summary>
/// Converts between the results an endpoint produces and the
/// <see cref="CachedResponse"/> stored in a backend.
/// </summary>
/// <remarks>
/// Bodies are serialized with the application's own JSON options, so a cached body is
/// byte-identical to what the action would have written — same naming policy, same
/// converters, same null handling.
/// </remarks>
public class CachedResponseFactory
{
    /// <summary>
    /// The content type used when a result carries a value but declares no content type.
    /// </summary>
    internal const string DefaultContentType = "application/json";

    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// A factory using default JSON options, for the filters that never write cache
    /// entries — eviction and refresh — so they need not carry a dependency they never use.
    /// </summary>
    internal static readonly CachedResponseFactory None = new(JsonSerializerOptions.Default);

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedResponseFactory"/> class.
    /// </summary>
    /// <param name="jsonOptions">The application's JSON options, used to render bodies.</param>
    public CachedResponseFactory(JsonSerializerOptions jsonOptions) => _jsonOptions = jsonOptions;

    /// <summary>
    /// Builds a cacheable response from an MVC action result.
    /// </summary>
    /// <param name="result">The result the action produced.</param>
    /// <param name="request">The request that produced it, recorded for refresh.</param>
    /// <param name="variesByRequest">Whether the cache key varies by request context.</param>
    /// <param name="cachedResponse">The resulting envelope when the result can be cached.</param>
    /// <returns><see langword="true"/> when the result can be represented as a cached response.</returns>
    public bool TryCreate(
        IActionResult result,
        CachedRequest? request,
        bool variesByRequest,
        out CachedResponse? cachedResponse)
    {
        switch (result)
        {
            case ContentResult content:
                cachedResponse = Create(
                    content.StatusCode ?? StatusCodes.Status200OK,
                    content.ContentType,
                    content.Content,
                    request,
                    variesByRequest);
                return true;

            // JsonResult is not an ObjectResult, but IsCacheableResult accepts it, so it
            // must be handled here or a cacheable result would silently stop being cached.
            case JsonResult jsonResult:
                cachedResponse = Create(
                    jsonResult.StatusCode ?? StatusCodes.Status200OK,
                    jsonResult.ContentType ?? DefaultContentType,
                    Serialize(jsonResult.Value),
                    request,
                    variesByRequest);
                return true;

            case ObjectResult objectResult:
                cachedResponse = Create(
                    objectResult.StatusCode ?? StatusCodes.Status200OK,
                    objectResult.ContentTypes.FirstOrDefault() ?? DefaultContentType,
                    Serialize(objectResult.Value),
                    request,
                    variesByRequest);
                return true;

            case StatusCodeResult statusCodeResult:
                cachedResponse = Create(statusCodeResult.StatusCode, null, null, request, variesByRequest);
                return true;

            default:
                // Results that do work at execution time — file streams, redirects — cannot
                // be reduced to a body. The previous implementation "cached" them by type
                // name and rebuilt an object holding, for a file result, a disposed stream.
                cachedResponse = null;
                return false;
        }
    }

    /// <summary>
    /// Builds a cacheable response from a Minimal API endpoint result.
    /// </summary>
    /// <param name="result">The result the endpoint produced.</param>
    /// <param name="request">The request that produced it, recorded for refresh.</param>
    /// <param name="variesByRequest">Whether the cache key varies by request context.</param>
    /// <param name="cachedResponse">The resulting envelope when the result can be cached.</param>
    /// <returns><see langword="true"/> when the result can be represented as a cached response.</returns>
    public bool TryCreateFromEndpointResult(
        object? result,
        CachedRequest? request,
        bool variesByRequest,
        out CachedResponse? cachedResponse)
    {
        if (result is null)
        {
            cachedResponse = null;
            return false;
        }

        var statusCode = result is IStatusCodeHttpResult statusCodeResult
            ? statusCodeResult.StatusCode ?? StatusCodes.Status200OK
            : StatusCodes.Status200OK;

        if (result is IValueHttpResult valueResult)
        {
            cachedResponse = Create(statusCode, DefaultContentType, Serialize(valueResult.Value), request, variesByRequest);
            return true;
        }

        if (result is IResult)
        {
            // An IResult with no value — a status-only result. Anything richer (a file, a
            // redirect) writes to the response itself and cannot be reduced to a body.
            cachedResponse = result is IStatusCodeHttpResult
                ? Create(statusCode, null, null, request, variesByRequest)
                : null;
            return cachedResponse is not null;
        }

        // A plain object returned straight from the endpoint.
        cachedResponse = Create(statusCode, DefaultContentType, Serialize(result), request, variesByRequest);
        return true;
    }

    /// <summary>
    /// Rebuilds an MVC result from a cached response.
    /// </summary>
    /// <param name="cachedResponse">The cached response.</param>
    /// <returns>A result that reproduces the cached status, content type and body.</returns>
    public static IActionResult ToActionResult(CachedResponse cachedResponse) =>
        new ContentResult
        {
            StatusCode = cachedResponse.StatusCode,
            ContentType = cachedResponse.ContentType,
            Content = cachedResponse.Body
        };

    /// <summary>
    /// Rebuilds a Minimal API result from a cached response.
    /// </summary>
    /// <param name="cachedResponse">The cached response.</param>
    /// <returns>A result that reproduces the cached status, content type and body.</returns>
    public static IResult ToEndpointResult(CachedResponse cachedResponse) =>
        Results.Text(
            cachedResponse.Body ?? string.Empty,
            cachedResponse.ContentType,
            statusCode: cachedResponse.StatusCode);

    /// <summary>
    /// Records the request line of the current request, for refresh to replay.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <returns>The recorded request line.</returns>
    public static CachedRequest CreateRequest(HttpContext httpContext) => new()
    {
        Method = httpContext.Request.Method,
        Path = httpContext.Request.Path.Value ?? "/",
        QueryString = httpContext.Request.QueryString.HasValue
            ? httpContext.Request.QueryString.Value
            : null
    };

    private string? Serialize(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, value.GetType(), _jsonOptions);

    private static CachedResponse Create(
        int statusCode,
        string? contentType,
        string? body,
        CachedRequest? request,
        bool variesByRequest) => new()
    {
        StatusCode = statusCode,
        ContentType = contentType,
        Body = body,
        Request = request,
        VariesByRequest = variesByRequest
    };
}

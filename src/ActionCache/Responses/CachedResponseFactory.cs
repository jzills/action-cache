using System.Text.Json;
using ActionCache.Common.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Net.Http.Headers;

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
    /// Records the current request so refresh can replay it.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="body">The bound body model, or <see langword="null"/> when the request had none.</param>
    /// <returns>
    /// The recorded request, or <see langword="null"/> when the request cannot be faithfully
    /// replayed and the entry should therefore not be refreshed.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The body is re-serialized from the bound model rather than read from the request
    /// stream, which model binding has already consumed by the time a cache filter runs.
    /// Reading the raw bytes instead would mean enabling buffering before binding, and so
    /// inserting middleware into the host's pipeline.
    /// </para>
    /// <para>
    /// Because the recorded body is JSON, the content type it is replayed with has to be one
    /// the endpoint will accept JSON for. The request's own content type is preserved when it
    /// is JSON-compatible, which covers versioned APIs declaring a vendor type such as
    /// <c>application/vnd.example.v1+json</c>: the payload always suited them and only the
    /// header did not, so replaying as <c>application/json</c> had them answer <c>415</c> on
    /// every pass.
    /// </para>
    /// <para>
    /// A body sent as something JSON cannot stand in for -- XML, a form post -- has no such
    /// rescue: no header makes re-serialized JSON bind to it. Rather than record a request that
    /// is certain to be rejected, none is recorded at all, which leaves the entry cached and
    /// unreplayable so refresh skips it and logs the skip once per pass.
    /// </para>
    /// </remarks>
    public CachedRequest? CreateRequest(HttpContext httpContext, object? body = null)
    {
        var contentType = httpContext.Request.ContentType;

        // Only a re-serialized body has to match a content type. Without one there is nothing
        // for the endpoint to reject, whatever the original request happened to declare.
        if (body is not null && !IsJsonCompatible(contentType))
        {
            return null;
        }

        return new CachedRequest
        {
            Method = httpContext.Request.Method,
            Path = httpContext.Request.Path.Value ?? "/",
            QueryString = httpContext.Request.QueryString.HasValue
                ? httpContext.Request.QueryString.Value
                : null,
            Body = body is null ? null : Serialize(body),
            ContentType = body is null ? null : contentType ?? DefaultContentType
        };
    }

    /// <summary>
    /// Whether a JSON payload can be sent under the given content type.
    /// </summary>
    /// <param name="contentType">The request's content type, or <see langword="null"/> when it declared none.</param>
    /// <returns><see langword="true"/> when a JSON body is acceptable under it.</returns>
    /// <remarks>
    /// True for <c>application/json</c> and <c>text/json</c>, for any type with a <c>+json</c>
    /// structured suffix, and for a request that declared no content type at all -- which is
    /// what the recorded request has always assumed and what the default covers.
    /// </remarks>
    private static bool IsJsonCompatible(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return true;
        }

        if (!MediaTypeHeaderValue.TryParse(contentType, out var mediaType))
        {
            return false;
        }

        return mediaType.Suffix.Equals("json", StringComparison.OrdinalIgnoreCase) ||
               mediaType.MatchesMediaType("application/json") ||
               mediaType.MatchesMediaType("text/json");
    }

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

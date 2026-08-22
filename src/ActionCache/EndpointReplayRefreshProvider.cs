using System.Globalization;
using ActionCache.Common.Diagnostics;
using ActionCache.Common.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ActionCache.Common.Caching;

/// <summary>
/// Refreshes a cache entry by re-issuing its recorded request against the matching
/// endpoint, in process.
/// </summary>
/// <remarks>
/// <para>
/// This executes the <em>endpoint</em>: model binding, action filters, the action itself,
/// and result execution, all with a real <see cref="HttpContext"/>. It does not run outer
/// middleware — authentication, CORS, exception handling — because those belong to the
/// request pipeline rather than the endpoint. That is a real limitation and worth knowing,
/// but it is a long way from the previous implementation, which invoked the controller
/// method by reflection as a plain object with no context at all.
/// </para>
/// <para>
/// Each replay runs in its own DI scope, so it cannot disturb the scoped services of the
/// request that triggered the refresh.
/// </para>
/// </remarks>
public class EndpointReplayRefreshProvider : IActionCacheRefreshProvider
{
    private readonly EndpointDataSource _endpointDataSource;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EndpointReplayRefreshProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EndpointReplayRefreshProvider"/> class.
    /// </summary>
    /// <param name="endpointDataSource">The application's endpoints, searched for a match.</param>
    /// <param name="scopeFactory">Creates the DI scope each replay runs in.</param>
    /// <param name="logger">Records replay outcomes.</param>
    public EndpointReplayRefreshProvider(
        EndpointDataSource endpointDataSource,
        IServiceScopeFactory scopeFactory,
        ILogger<EndpointReplayRefreshProvider> logger)
    {
        _endpointDataSource = endpointDataSource;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CachedResponse?> ReplayAsync(
        CachedRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryMatchEndpoint(request, out var endpoint, out var routeValues))
        {
            ActionCacheLog.RefreshReplayNoEndpoint(_logger, request.Method, request.Path);
            return null;
        }

        using var activity = ActionCacheDiagnostics.StartOperation("RefreshReplay", request.Path);
        using var scope = _scopeFactory.CreateScope();
        using var body = new MemoryStream();

        var httpContext = CreateHttpContext(request, scope, body, routeValues, endpoint!, cancellationToken);

        await endpoint!.RequestDelegate!(httpContext);

        var statusCode = httpContext.Response.StatusCode;
        activity?.SetTag("http.response.status_code", statusCode);

        // Only a successful replay may replace an entry. A transient 500, a 404 from a
        // deleted resource, or a binding failure would otherwise overwrite a good cached
        // response with an error and serve it until expiry — refresh actively making things
        // worse than doing nothing.
        if (statusCode < StatusCodes.Status200OK || statusCode > StatusCodes.Status226IMUsed)
        {
            ActionCacheLog.RefreshReplayNotSuccessful(_logger, request.Method, request.Path, statusCode);
            return null;
        }

        return new CachedResponse
        {
            StatusCode = statusCode,
            ContentType = httpContext.Response.ContentType,
            Body = ReadBody(body),
            Request = request
        };
    }

    private bool TryMatchEndpoint(
        CachedRequest request,
        out RouteEndpoint? endpoint,
        out RouteValueDictionary routeValues)
    {
        routeValues = [];

        foreach (var candidate in _endpointDataSource.Endpoints.OfType<RouteEndpoint>())
        {
            if (candidate.RequestDelegate is null || !AcceptsMethod(candidate, request.Method))
            {
                continue;
            }

            var values = new RouteValueDictionary();
            var matcher = new TemplateMatcher(
                new RouteTemplate(candidate.RoutePattern),
                new RouteValueDictionary(candidate.RoutePattern.Defaults));

            if (matcher.TryMatch(request.Path, values) && SatisfiesRequiredValues(candidate, values))
            {
                endpoint = candidate;
                routeValues = values;
                return true;
            }
        }

        endpoint = null;
        return false;
    }

    /// <summary>
    /// Checks the endpoint's required route values against what the path actually matched.
    /// </summary>
    /// <param name="endpoint">The candidate endpoint.</param>
    /// <param name="values">The route values the template matcher produced.</param>
    /// <returns><see langword="true"/> when the endpoint is a genuine match for those values.</returns>
    /// <remarks>
    /// Conventional routing gives every action the same pattern — <c>{controller}/{action}</c>
    /// — and distinguishes them only by required values. Matching on the template alone
    /// therefore returns whichever action happens to come first, and every entry in the
    /// namespace gets refreshed with that one action's response. Attribute-routed endpoints
    /// carry controller and action as required values too, but their patterns bind neither,
    /// so a key the match did not produce is left alone rather than treated as a mismatch.
    /// </remarks>
    private static bool SatisfiesRequiredValues(RouteEndpoint endpoint, RouteValueDictionary values)
    {
        foreach (var required in endpoint.RoutePattern.RequiredValues)
        {
            // MVC's required values are strings; the "matches any value" sentinel is a
            // private object, so anything that is not a non-empty string constrains nothing.
            if (required.Value is not string expected || expected.Length == 0)
            {
                continue;
            }

            // A key the pattern does not bind — controller and action on an attribute-routed
            // endpoint — never appears in the match and is not a mismatch.
            if (!values.TryGetValue(required.Key, out var matched))
            {
                continue;
            }

            if (!string.Equals(
                    Convert.ToString(matched, CultureInfo.InvariantCulture),
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AcceptsMethod(Endpoint endpoint, string method)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;

        // No method metadata means the endpoint accepts anything.
        return methods is null or { Count: 0 } ||
               methods.Contains(method, StringComparer.OrdinalIgnoreCase);
    }

    private static HttpContext CreateHttpContext(
        CachedRequest request,
        IServiceScope scope,
        Stream body,
        RouteValueDictionary routeValues,
        Endpoint endpoint,
        CancellationToken cancellationToken)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            RequestAborted = cancellationToken
        };

        httpContext.Request.Method = request.Method;
        httpContext.Request.Path = request.Path;
        httpContext.Request.QueryString = request.QueryString is null
            ? QueryString.Empty
            : new QueryString(request.QueryString);

        if (request.Body is not null)
        {
            var payload = System.Text.Encoding.UTF8.GetBytes(request.Body);
            httpContext.Request.Body = new MemoryStream(payload);
            httpContext.Request.ContentLength = payload.Length;
            httpContext.Request.ContentType = request.ContentType;
        }

        httpContext.Response.Body = body;
        httpContext.Features.Set<IRoutingFeature>(new RoutingFeature { RouteData = new RouteData(routeValues) });
        httpContext.Request.RouteValues = routeValues;
        httpContext.SetEndpoint(endpoint);

        // Without this the replay would be served by the very filter it is refreshing.
        ActionCacheReplayMarker.Mark(httpContext);

        return httpContext;
    }

    private static string? ReadBody(MemoryStream body)
    {
        if (body.Length == 0)
        {
            return null;
        }

        body.Position = 0;
        using var reader = new StreamReader(body, leaveOpen: true);
        return reader.ReadToEnd();
    }
}

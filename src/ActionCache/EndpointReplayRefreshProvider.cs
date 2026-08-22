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
        activity?.SetTag("http.response.status_code", httpContext.Response.StatusCode);

        return new CachedResponse
        {
            StatusCode = httpContext.Response.StatusCode,
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

            if (matcher.TryMatch(request.Path, values))
            {
                endpoint = candidate;
                routeValues = values;
                return true;
            }
        }

        endpoint = null;
        return false;
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

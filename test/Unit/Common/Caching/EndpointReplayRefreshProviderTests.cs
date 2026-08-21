using ActionCache.Common.Caching;
using ActionCache.Common.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Unit.Common.Caching;

[TestFixture]
public class EndpointReplayRefreshProviderTests
{
    private sealed class StaticEndpointDataSource : EndpointDataSource
    {
        private readonly List<Endpoint> _endpoints;

        public StaticEndpointDataSource(params Endpoint[] endpoints) => _endpoints = [.. endpoints];

        public override IReadOnlyList<Endpoint> Endpoints => _endpoints;

        public override Microsoft.Extensions.Primitives.IChangeToken GetChangeToken() =>
            new Microsoft.Extensions.Primitives.CancellationChangeToken(CancellationToken.None);
    }

    /// <summary>A scoped marker used to prove each replay gets its own DI scope.</summary>
    private sealed class ScopedMarker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private static RouteEndpoint Endpoint(
        string pattern,
        RequestDelegate handler,
        string? method = "GET") =>
        new(handler,
            RoutePatternFactory.Parse(pattern),
            order: 0,
            new EndpointMetadataCollection(method is null
                ? []
                : new object[] { new HttpMethodMetadata([method]) }),
            displayName: pattern);

    private static (EndpointReplayRefreshProvider Provider, IServiceProvider Services) Build(params Endpoint[] endpoints)
    {
        var services = new ServiceCollection()
            .AddScoped<ScopedMarker>()
            .BuildServiceProvider();

        var provider = new EndpointReplayRefreshProvider(
            new StaticEndpointDataSource(endpoints),
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EndpointReplayRefreshProvider>.Instance);

        return (provider, services);
    }

    [Test]
    public async Task ReplayAsync_ReturnsTheResponseTheEndpointProduced()
    {
        var (provider, _) = Build(Endpoint("users/me", async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"name":"alice"}""");
        }));

        var replayed = await provider.ReplayAsync(new CachedRequest { Method = "GET", Path = "/users/me" });

        replayed.Should().NotBeNull();
        replayed!.StatusCode.Should().Be(200);
        replayed.ContentType.Should().Be("application/json");
        replayed.Body.Should().Be("""{"name":"alice"}""");
    }

    [Test]
    public async Task ReplayAsync_GivesTheEndpointARealHttpContext()
    {
        // The regression for finding #4. The reflection implementation invoked the
        // controller method as a plain object with no context at all, so an action reading
        // HttpContext.Request threw a NullReferenceException.
        string? observedPath = null;
        string? observedQuery = null;
        string? observedMethod = null;

        var (provider, _) = Build(Endpoint("users/me", context =>
        {
            observedPath = context.Request.Path;
            observedQuery = context.Request.Query["page"];
            observedMethod = context.Request.Method;
            return Task.CompletedTask;
        }));

        await provider.ReplayAsync(new CachedRequest
        {
            Method = "GET",
            Path = "/users/me",
            QueryString = "?page=2"
        });

        observedPath.Should().Be("/users/me");
        observedQuery.Should().Be("2");
        observedMethod.Should().Be("GET");
    }

    [Test]
    public async Task ReplayAsync_PopulatesRouteValuesFromTheMatchedTemplate()
    {
        object? observedId = null;

        var (provider, _) = Build(Endpoint("accounts/{id}", context =>
        {
            observedId = context.Request.RouteValues["id"];
            return Task.CompletedTask;
        }));

        await provider.ReplayAsync(new CachedRequest { Method = "GET", Path = "/accounts/42" });

        observedId.Should().Be("42");
    }

    [Test]
    public async Task ReplayAsync_RunsInItsOwnScope()
    {
        var observed = new List<Guid>();
        var (provider, _) = Build(Endpoint("scoped", context =>
        {
            observed.Add(context.RequestServices.GetRequiredService<ScopedMarker>().Id);
            return Task.CompletedTask;
        }));

        await provider.ReplayAsync(new CachedRequest { Method = "GET", Path = "/scoped" });
        await provider.ReplayAsync(new CachedRequest { Method = "GET", Path = "/scoped" });

        observed[0].Should().NotBe(observed[1], "each replay must get a fresh scope");
    }

    [Test]
    public async Task ReplayAsync_WhenNoEndpointMatches_ReturnsNullRatherThanThrowing()
    {
        var (provider, _) = Build(Endpoint("users/me", _ => Task.CompletedTask));

        var replayed = await provider.ReplayAsync(new CachedRequest { Method = "GET", Path = "/nope" });

        replayed.Should().BeNull();
    }

    [Test]
    public async Task ReplayAsync_WhenTheMethodDoesNotMatch_ReturnsNull()
    {
        var (provider, _) = Build(Endpoint("users/me", _ => Task.CompletedTask, method: "POST"));

        var replayed = await provider.ReplayAsync(new CachedRequest { Method = "GET", Path = "/users/me" });

        replayed.Should().BeNull();
    }

    [Test]
    public async Task ReplayAsync_CarriesTheRecordedRequestOntoTheRefreshedEntry()
    {
        var (provider, _) = Build(Endpoint("users/me", context => context.Response.WriteAsync("ok")));
        var request = new CachedRequest { Method = "GET", Path = "/users/me" };

        var replayed = await provider.ReplayAsync(request);

        replayed!.Request.Should().Be(request, "the refreshed entry must stay refreshable");
    }
}

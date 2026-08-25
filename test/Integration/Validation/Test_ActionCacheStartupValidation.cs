using ActionCache.Common.Extensions;
using ActionCache.EndpointFilters.Extensions;
using ActionCache.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Validation;

/// <summary>
/// Conflicting cache attributes fail the host at startup rather than misbehaving quietly per
/// request. Every case here is one that previously started cleanly and then did the wrong
/// thing — an eviction that never ran, a cache that never served.
/// </summary>
[TestFixture]
public class Test_ActionCacheStartupValidation
{
    private static WebApplication Build(Action<WebApplication> map)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddActionCache(options => options.UseMemoryCache(cacheOptions => { }));

        var app = builder.Build();
        app.UseRouting();
        map(app);
        return app;
    }

    [Test]
    public void Startup_WhenAnEndpointCachesAndEvicts_Throws()
    {
        var app = Build(a => a.MapGet("/x", () => "x")
            .WithActionCache("A")
            .WithActionCacheEviction("B"));

        var exception = Assert.ThrowsAsync<ConflictingCacheAttributesException>(() => app.StartAsync());

        Assert.That(exception!.Message, Does.Contain("side effect"));
    }

    [Test]
    public void Startup_WhenAnEndpointCachesAndRefreshes_Throws()
    {
        var app = Build(a => a.MapGet("/x", () => "x")
            .WithActionCache("A")
            .WithActionCacheRefresh("B"));

        Assert.ThrowsAsync<ConflictingCacheAttributesException>(() => app.StartAsync());
    }

    [Test]
    public void Startup_WhenTwoSideEffectsShareANamespace_Throws()
    {
        var app = Build(a => a.MapPost("/x", () => Results.Ok())
            .WithActionCacheRefresh("Shared")
            .WithActionCacheEviction("Shared"));

        var exception = Assert.ThrowsAsync<ConflictingCacheAttributesException>(() => app.StartAsync());

        Assert.That(exception!.Message, Does.Contain("Shared"));
    }

    [Test]
    public void Startup_NamesTheOffendingEndpoint()
    {
        var app = Build(a => a.MapGet("/offending-route", () => "x")
            .WithActionCache("A")
            .WithActionCacheEviction("B"));

        var exception = Assert.ThrowsAsync<ConflictingCacheAttributesException>(() => app.StartAsync());

        // Without the route in the message, an application with many endpoints gives the
        // author a rule violation and no way to find it.
        Assert.That(exception!.Message, Does.Contain("/offending-route"));
    }

    [Test]
    public async Task Startup_WhenSideEffectsTargetDifferentNamespaces_Succeeds()
    {
        // The combination worth supporting: one write warms a namespace and clears others.
        var app = Build(a =>
        {
            a.MapGet("/read", () => "x").WithActionCache("Warm");
            a.MapPost("/write", () => Results.Ok())
                .WithActionCacheRefresh("Warm")
                .WithActionCacheEviction("ColdOne")
                .WithActionCacheEviction("ColdTwo");
        });

        await app.StartAsync();
        await app.StopAsync();
    }

    [Test]
    public async Task Startup_WithControllers_Succeeds()
    {
        // MVC adds each action attribute to endpoint metadata twice, as the same instance.
        // Counting them without de-duplicating would fail every controller application that
        // uses the library at all.
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(Integration.Controllers.UsersController).Assembly);
        builder.Services.AddActionCache(options => options.UseMemoryCache(cacheOptions => { }));

        var app = builder.Build();
        app.UseRouting();
        app.MapControllers();

        await app.StartAsync();
        await app.StopAsync();
    }
}

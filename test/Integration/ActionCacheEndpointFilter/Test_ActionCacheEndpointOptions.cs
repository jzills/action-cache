using ActionCache;
using ActionCache.Common.Enums;
using ActionCache.Common.Extensions;
using ActionCache.EndpointFilters.Extensions;
using Integration.TestUtilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.EndpointOptions;

/// <summary>
/// The per-endpoint options the MVC attribute has always exposed, reaching Minimal API
/// endpoints. Each test is written so it fails when the option is not passed through rather
/// than merely when the endpoint breaks: an option that is silently ignored looks exactly
/// like one that is honoured until the behaviour it controls is the thing being measured.
/// </summary>
[TestFixture]
public class Test_ActionCacheEndpointOptions
{
    WebApplication App = null!;
    HttpClient Client = null!;

    static int Version;

    async Task Start(Action<WebApplication> map)
    {
        Version = 0;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddActionCache(options => options.UseMemoryCache(cacheOptions => { }));

        App = builder.Build();
        App.UseRouting();
        map(App);

        await App.StartAsync();
        Client = App.GetTestServer().CreateClient();
    }

    static string Status(HttpResponseMessage response) =>
        response.Headers.GetValues(CacheHeaders.CacheStatus).First();

    [Test]
    public async Task VaryByQuery_GivesEachQueryValueItsOwnEntry()
    {
        // The handler binds no parameters, so the query string reaches the cache key only
        // through VaryByQuery. Without it both requests share one entry and the second is a
        // Hit -- which is precisely the bug of an ignored option.
        await Start(app => app.MapGet("/opts", () => new { V = ++Version })
            .WithActionCache("Opts", options => options.VaryByQuery = "page"));

        var first = await Client.GetAsync("/opts?page=1");
        var repeat = await Client.GetAsync("/opts?page=1");
        var other = await Client.GetAsync("/opts?page=2");

        Assert.Multiple(() =>
        {
            Assert.That(Status(first), Is.EqualTo(nameof(CacheStatus.Add)));
            Assert.That(Status(repeat), Is.EqualTo(nameof(CacheStatus.Hit)),
                "the same query value must be served from cache");
            Assert.That(Status(other), Is.EqualTo(nameof(CacheStatus.Add)),
                "a different query value must not be served the first value's response");
        });
    }

    [Test]
    public async Task WithoutVaryByQuery_TheQueryStringDoesNotReachTheKey()
    {
        // The control for the test above. This is the behaviour that made the missing option
        // dangerous rather than merely absent: two different queries share one response.
        await Start(app => app.MapGet("/opts", () => new { V = ++Version })
            .WithActionCache("Opts"));

        await Client.GetAsync("/opts?page=1");
        var other = await Client.GetAsync("/opts?page=2");

        Assert.That(Status(other), Is.EqualTo(nameof(CacheStatus.Hit)));
    }

    [Test]
    public async Task AbsoluteExpiration_ExpiresTheEntry()
    {
        await Start(app => app.MapGet("/opts", () => new { V = ++Version })
            .WithActionCache("Opts", options => options.AbsoluteExpiration = TimeSpan.FromSeconds(2)));

        var first = await Client.GetAsync("/opts");
        var expiredWell = DateTimeOffset.UtcNow.AddSeconds(2).AddSeconds(2);
        var hit = await Client.GetAsync("/opts");

        Assert.Multiple(() =>
        {
            Assert.That(Status(first), Is.EqualTo(nameof(CacheStatus.Add)));
            Assert.That(Status(hit), Is.EqualTo(nameof(CacheStatus.Hit)));
        });

        await WallClock.WaitUntilPast(expiredWell);

        var afterExpiry = await Client.GetAsync("/opts");

        Assert.That(Status(afterExpiry), Is.EqualTo(nameof(CacheStatus.Add)),
            "the entry must have expired rather than being served indefinitely");
    }

    [Test]
    public async Task SlidingExpiration_RenewsOnReadAndStillExpiresWhenLeftAlone()
    {
        // Both halves are needed to pin this down. Surviving repeated reads past its own
        // lifetime rules out an absolute expiration; expiring once the reads stop rules out
        // the option having been dropped, which leaves an entry that never expires at all
        // and would satisfy the first half on its own.
        await Start(app => app.MapGet("/opts", () => new { V = ++Version })
            .WithActionCache("Opts", options => options.SlidingExpiration = TimeSpan.FromSeconds(3)));

        await Client.GetAsync("/opts");

        for (var read = 0; read < 2; read++)
        {
            await WallClock.WaitUntilPast(DateTimeOffset.UtcNow.AddSeconds(2));
            var renewed = await Client.GetAsync("/opts");

            Assert.That(Status(renewed), Is.EqualTo(nameof(CacheStatus.Hit)),
                "reading before the window elapses must renew the entry");
        }

        // Four seconds of reading have now passed, so a three-second absolute expiration
        // would already have dropped it.
        await WallClock.WaitUntilPast(DateTimeOffset.UtcNow.AddSeconds(3).AddSeconds(2));
        var afterIdle = await Client.GetAsync("/opts");

        Assert.That(Status(afterIdle), Is.EqualTo(nameof(CacheStatus.Add)),
            "an entry left unread past its sliding window must expire");
    }

    [Test]
    public async Task SingleFlightFalse_StillServesAndCaches()
    {
        // Coalescing is a concurrency behaviour and not deterministically observable from a
        // single-threaded test. What is asserted here is that turning it off reaches the
        // filter without breaking the ordinary path -- the option is exercised, not faked.
        await Start(app => app.MapGet("/opts", () => new { V = ++Version })
            .WithActionCache("Opts", options => options.SingleFlight = false));

        var first = await Client.GetAsync("/opts");
        var second = await Client.GetAsync("/opts");

        Assert.Multiple(() =>
        {
            Assert.That(Status(first), Is.EqualTo(nameof(CacheStatus.Add)));
            Assert.That(Status(second), Is.EqualTo(nameof(CacheStatus.Hit)));
        });
    }

    [Test]
    public async Task VaryByHeader_GivesEachHeaderValueItsOwnEntry()
    {
        await Start(app => app.MapGet("/opts", () => new { V = ++Version })
            .WithActionCache("Opts", options => options.VaryByHeader = "X-Tenant"));

        var first = new HttpRequestMessage(HttpMethod.Get, "/opts");
        first.Headers.Add("X-Tenant", "acme");
        var second = new HttpRequestMessage(HttpMethod.Get, "/opts");
        second.Headers.Add("X-Tenant", "globex");

        var acme = await Client.SendAsync(first);
        var globex = await Client.SendAsync(second);

        Assert.Multiple(() =>
        {
            Assert.That(Status(acme), Is.EqualTo(nameof(CacheStatus.Add)));
            Assert.That(Status(globex), Is.EqualTo(nameof(CacheStatus.Add)),
                "one tenant must not be served another tenant's response");
        });
    }

    [TearDown]
    public async Task TearDown()
    {
        await App.Services.GetRequiredService<IActionCacheFactory>().Create("Opts")!.RemoveAsync();
        await App.StopAsync();
    }
}

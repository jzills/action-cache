using System.Reflection;
using ActionCache;
using ActionCache.Attributes;
using ActionCache.Common.Enums;
using ActionCache.Common.Extensions;
using ActionCache.EndpointFilters.Extensions;
using Integration.TestUtilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.RefreshExpiration;

/// <summary>
/// A refresh must rewrite an entry with the expiration its own endpoint declared.
///
/// The refresh filter is created without expirations, so writing through its own options took
/// the global <c>UseEntryOptions</c> defaults instead — and with none configured, that is no
/// expiration at all. One refresh was enough to turn a bounded entry permanent, and a namespace
/// refreshed on every write never expired anything.
///
/// No global entry options are configured in these fixtures, so the difference is stark: either
/// the declared expiration survives the refresh, or the entry becomes immortal.
/// </summary>
[TestFixture]
public class Test_ActionCache_RefreshPreservesExpiration
{
    const int ExpirationSeconds = 3;

    WebApplication App = null!;
    HttpClient Client = null!;

    static string Status(HttpResponseMessage response) =>
        response.Headers.GetValues(CacheHeaders.CacheStatus).First();

    /// <summary>Waits well past the declared expiration, measured from now.</summary>
    static Task WaitPastExpiration() =>
        WallClock.WaitUntilPast(DateTimeOffset.UtcNow.AddSeconds(ExpirationSeconds).AddSeconds(2));

    [Test]
    public async Task MinimalApi_RefreshedEntry_StillExpires()
    {
        var version = 0;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddActionCache(options => options.UseMemoryCache(cacheOptions => { }));

        App = builder.Build();
        App.UseRouting();
        App.MapGet("/data", () => new { V = ++version })
            .WithActionCache("RefreshExpiry",
                options => options.AbsoluteExpiration = TimeSpan.FromSeconds(ExpirationSeconds));
        App.MapPost("/refresh", () => Results.Ok())
            .WithActionCacheRefresh("RefreshExpiry");

        await App.StartAsync();
        Client = App.GetTestServer().CreateClient();

        var first = await Client.GetAsync("/data");
        Assert.That(Status(first), Is.EqualTo(nameof(CacheStatus.Add)));

        var refresh = await Client.PostAsync("/refresh", content: null);
        Assert.That(Status(refresh), Is.EqualTo(nameof(CacheStatus.Refresh)));

        await WaitPastExpiration();

        var afterExpiry = await Client.GetAsync("/data");

        Assert.That(Status(afterExpiry), Is.EqualTo(nameof(CacheStatus.Add)),
            "a refreshed entry must keep the expiration its endpoint declared, not become permanent");
    }

    [Test]
    public async Task Mvc_RefreshedEntry_StillExpires()
    {
        RefreshExpiryController.Version = 0;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(Assembly.GetExecutingAssembly());
        builder.Services.AddActionCache(options => options.UseMemoryCache(cacheOptions => { }));

        App = builder.Build();
        App.UseRouting();
        App.MapControllers();

        await App.StartAsync();
        Client = App.GetTestServer().CreateClient();

        var first = await Client.GetAsync("refresh-expiry/data");
        Assert.That(Status(first), Is.EqualTo(nameof(CacheStatus.Add)));

        var refresh = await Client.PostAsync("refresh-expiry/refresh", content: null);
        Assert.That(Status(refresh), Is.EqualTo(nameof(CacheStatus.Refresh)));

        await WaitPastExpiration();

        var afterExpiry = await Client.GetAsync("refresh-expiry/data");

        Assert.That(Status(afterExpiry), Is.EqualTo(nameof(CacheStatus.Add)),
            "the attribute path must preserve the declared expiration across a refresh too");
    }

    [Test]
    public async Task MinimalApi_RefreshedEntry_IsStillTheRefreshedValue()
    {
        // Preserving the expiration must not come at the cost of the refresh itself: the entry
        // has to hold the replayed response, not the one recorded before the refresh.
        var version = 0;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddActionCache(options => options.UseMemoryCache(cacheOptions => { }));

        App = builder.Build();
        App.UseRouting();
        App.MapGet("/data", () => new { V = ++version })
            .WithActionCache("RefreshExpiry",
                options => options.AbsoluteExpiration = TimeSpan.FromSeconds(30));
        App.MapPost("/refresh", () => Results.Ok())
            .WithActionCacheRefresh("RefreshExpiry");

        await App.StartAsync();
        Client = App.GetTestServer().CreateClient();

        await Client.GetAsync("/data");
        await Client.PostAsync("/refresh", content: null);

        var afterRefresh = await Client.GetAsync("/data");

        Assert.Multiple(async () =>
        {
            Assert.That(Status(afterRefresh), Is.EqualTo(nameof(CacheStatus.Hit)));
            Assert.That(await afterRefresh.Content.ReadAsStringAsync(), Does.Contain("\"v\":2"));
        });
    }

    [TearDown]
    public async Task TearDown()
    {
        await App.Services.GetRequiredService<IActionCacheFactory>().Create("RefreshExpiry")!.RemoveAsync();
        await App.StopAsync();
    }
}

/// <summary>
/// The attribute half of <see cref="Test_ActionCache_RefreshPreservesExpiration"/>: the same
/// arrangement declared on a controller, since both hosting models write through the same
/// refresh path.
/// </summary>
[ApiController]
[Route("refresh-expiry")]
public class RefreshExpiryController : ControllerBase
{
    public static int Version;

    [HttpGet("data")]
    [ActionCache(Namespace = "RefreshExpiry", AbsoluteExpiration = 3000)]
    public IActionResult Data() => Ok(new { V = ++Version });

    [HttpPost("refresh")]
    [ActionCacheRefresh(Namespace = "RefreshExpiry")]
    public IActionResult Refresh() => Ok();
}

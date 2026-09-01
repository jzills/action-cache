using System.Reflection;
using ActionCache;
using ActionCache.Attributes;
using ActionCache.Common.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.ThrowingReplay;

/// <summary>
/// Caches fine, then throws once <see cref="Broken"/> is set — standing in for a resource
/// that was deleted, or a dependency that went down, between caching and refreshing.
/// </summary>
[Route("throwing")]
public class ThrowingController : Controller
{
    public static bool Broken;
    public static string Value = "original";

    [HttpGet("stable/{id}")]
    [ActionCache(Namespace = "Throwing")]
    public IActionResult GetStable(string id) => Ok(new { Id = id, Value });

    [HttpGet("fragile")]
    [ActionCache(Namespace = "Throwing")]
    public IActionResult GetFragile()
    {
        if (Broken)
        {
            throw new InvalidOperationException("the underlying resource is gone");
        }

        return Ok(new { Id = "fragile", Value });
    }
}

[TestFixture]
public class Test_ActionCache_RefreshReplay_ThrowingAction
{
    WebApplication App;
    HttpClient Client;

    [SetUp]
    public async Task Setup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMvc()
            .AddApplicationPart(Assembly.GetExecutingAssembly());
        builder.Services.AddActionCache(options => options.UseMemoryCache(memory => { }));

        App = builder.Build();
        App.UseRouting();
        App.MapControllers();

        await App.StartAsync();
        Client = App.GetTestServer().CreateClient();
    }

    [Test]
    public async Task Refresh_WhenOneActionThrows_StillRefreshesTheOtherEntries()
    {
        // Regression: the replay executes the endpoint, so a throwing action propagated out
        // of the refresh loop and every key after it in the namespace went unrefreshed —
        // silently, since the caller of RefreshAsync is usually a fire-and-forget filter.
        ThrowingController.Broken = false;
        ThrowingController.Value = "original";

        // Three entries in one namespace. The fragile one sits between two stable ones so a
        // pass that aborts on it leaves the third visibly stale.
        foreach (var path in new[] { "throwing/stable/1", "throwing/fragile", "throwing/stable/2" })
        {
            (await Client.GetAsync(path)).EnsureSuccessStatusCode();
        }

        ThrowingController.Value = "refreshed";
        ThrowingController.Broken = true;

        var cache = App.Services.GetRequiredService<IActionCacheFactory>().Create("Throwing")!;

        Assert.DoesNotThrowAsync(async () => await cache.RefreshAsync(),
            "one bad entry must not fail the whole pass");

        var first = await (await Client.GetAsync("throwing/stable/1")).Content.ReadAsStringAsync();
        var second = await (await Client.GetAsync("throwing/stable/2")).Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(first, Does.Contain("refreshed"), "the entry before the failure must be refreshed");
            Assert.That(second, Does.Contain("refreshed"), "the entry after the failure must be refreshed too");
        });

        // The failed entry keeps serving what it had rather than being emptied or corrupted.
        ThrowingController.Broken = false;
        var fragile = await Client.GetAsync("throwing/fragile");
        Assert.That((int)fragile.StatusCode, Is.EqualTo(200));
        Assert.That(await fragile.Content.ReadAsStringAsync(), Does.Contain("original"),
            "the entry whose replay threw must be left as it was");
    }

    [TearDown]
    public async Task TearDown()
    {
        ThrowingController.Broken = false;

        var cache = App.Services.GetRequiredService<IActionCacheFactory>().Create("Throwing");
        if (cache is not null)
        {
            await cache.RemoveAsync();
        }

        await App.StopAsync();
    }
}

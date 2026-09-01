using System.Reflection;
using ActionCache;
using ActionCache.Attributes;
using ActionCache.Common.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.ConventionalRouting;

/// <summary>
/// A conventionally routed controller. Its actions carry no route template of their own,
/// so every action in the app shares one route pattern.
/// </summary>
public class AlphaController : Controller
{
    public static string Value = "alpha-original";

    [ActionCache(Namespace = "Conventional")]
    public IActionResult Index() => Ok(new { Who = "alpha", Value });
}

/// <summary>
/// A second conventionally routed controller, indistinguishable from the first by route
/// template alone.
/// </summary>
public class BetaController : Controller
{
    public static string Value = "beta-original";

    [ActionCache(Namespace = "Conventional")]
    public IActionResult Index() => Ok(new { Who = "beta", Value });
}

[TestFixture]
public class Test_ActionCache_RefreshReplay_ConventionalRouting
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
        App.MapControllerRoute("default", "{controller}/{action}");

        await App.StartAsync();
        Client = App.GetTestServer().CreateClient();
    }

    [Test]
    public async Task Refresh_WithConventionalRouting_ReplaysEachEntrysOwnAction()
    {
        // Regression: conventional routing gives every action the same route pattern
        // ({controller}/{action}) and distinguishes them only by the endpoint's required
        // route values. Matching on the template alone returned whichever action came first
        // in the endpoint list, so refreshing this namespace rewrote *both* entries with
        // alpha's response and beta's callers were served alpha's data until expiry.
        AlphaController.Value = "alpha-original";
        BetaController.Value = "beta-original";

        var alphaFirst = await Client.GetAsync("/Alpha/Index");
        var betaFirst = await Client.GetAsync("/Beta/Index");
        alphaFirst.EnsureSuccessStatusCode();
        betaFirst.EnsureSuccessStatusCode();

        Assert.That(await alphaFirst.Content.ReadAsStringAsync(), Does.Contain("alpha-original"));
        Assert.That(await betaFirst.Content.ReadAsStringAsync(), Does.Contain("beta-original"));

        // Move the source data so a refreshed entry is distinguishable from a stale one,
        // and a cross-wired one from either.
        AlphaController.Value = "alpha-refreshed";
        BetaController.Value = "beta-refreshed";

        await App.Services.GetRequiredService<IActionCacheFactory>()
            .Create("Conventional")!.RefreshAsync();

        var alphaBody = await (await Client.GetAsync("/Alpha/Index")).Content.ReadAsStringAsync();
        var betaBody = await (await Client.GetAsync("/Beta/Index")).Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(alphaBody, Does.Contain("\"alpha\""), "alpha's entry must still hold alpha's action");
            Assert.That(betaBody, Does.Contain("\"beta\""), "beta's entry must not be served alpha's response");
            Assert.That(alphaBody, Does.Contain("alpha-refreshed"), "alpha's entry must be refreshed");
            Assert.That(betaBody, Does.Contain("beta-refreshed"), "beta's entry must be refreshed");
        });
    }

    [TearDown]
    public async Task TearDown()
    {
        var cache = App.Services.GetRequiredService<IActionCacheFactory>().Create("Conventional");
        if (cache is not null)
        {
            await cache.RemoveAsync();
        }

        await App.StopAsync();
    }
}

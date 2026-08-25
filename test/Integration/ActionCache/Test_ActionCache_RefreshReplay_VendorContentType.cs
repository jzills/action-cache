using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ActionCache;
using ActionCache.Common.Extensions;
using Integration.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A cached action declaring a vendor JSON media type -- the shape a versioned API takes.
/// The recorded body was always valid for it; only the content type recorded alongside was
/// wrong, so every replay was answered 415 and refresh never once replaced an entry.
/// </summary>
[TestFixture]
public class Test_ActionCache_RefreshReplay_VendorContentType
{
    const string VendorContentType = "application/vnd.example.v1+json";

    WebApplication App;
    HttpClient Client;

    [SetUp]
    public async Task Setup()
    {
        UsersController.RefreshableVendorValue = "original";

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMvc().AddApplicationPart(Assembly.GetExecutingAssembly());
        builder.Services.AddActionCache(options => options.UseRedisCache("127.0.0.1:6379"));

        App = builder.Build();
        App.UseRouting();
        App.MapControllers();

        await App.StartAsync();
        Client = App.GetTestServer().CreateClient();
    }

    private static StringContent VendorBody(bool showAll) =>
        new(JsonSerializer.Serialize(new { showAll }), Encoding.UTF8, VendorContentType);

    [Test]
    public async Task Refresh_WithAVendorJsonContentType_ReplacesTheEntry()
    {
        var first = await Client.PostAsync("users/query-vendor", VendorBody(showAll: true));
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await first.Content.ReadAsStringAsync(), Does.Contain("original"));

        UsersController.RefreshableVendorValue = "updated";

        var refresh = await Client.PostAsync("users/query-vendor/refresh", null);
        refresh.EnsureSuccessStatusCode();

        var afterRefresh = await Client.PostAsync("users/query-vendor", VendorBody(showAll: true));
        var body = await afterRefresh.Content.ReadAsStringAsync();

        Assert.That((int)afterRefresh.StatusCode, Is.EqualTo(200));
        Assert.That(body, Does.Contain("updated"),
            "the replay must be accepted by [Consumes], which it only is when the recorded " +
            "content type is the vendor type rather than application/json");
        Assert.That(body, Does.Contain("true"),
            "the replayed body must carry the original query values, not defaults");
    }

    [TearDown]
    public async Task TearDown()
    {
        var factory = App.Services.GetRequiredService<IActionCacheFactory>();
        await factory.Create("VendorReplay")!.RemoveAsync();
        await App.StopAsync();
    }
}

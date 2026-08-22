using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ActionCache;
using ActionCache.Common.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

[TestFixture]
public class Test_ActionCache_VaryByUser
{
    private const string SchemeName = "TestAuth";
    private const string UserHeader = "X-Test-User";

    /// <summary>
    /// Authenticates whoever the X-Test-User header names, so one client can act as
    /// several distinct users.
    /// </summary>
    private sealed class HeaderAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public HeaderAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var user) || string.IsNullOrEmpty(user))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, user!), new Claim(ClaimTypes.Name, user!)],
                SchemeName);

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    WebApplication App;
    HttpClient Client;

    [SetUp]
    public async Task Setup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMvc().AddApplicationPart(Assembly.GetExecutingAssembly());
        builder.Services
            .AddAuthentication(SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(SchemeName, _ => { });
        builder.Services.AddActionCache(options => options.UseRedisCache("127.0.0.1:6379"));

        App = builder.Build();
        App.UseRouting();
        App.UseAuthentication();
        App.MapControllers();

        await App.StartAsync();
        Client = App.GetTestServer().CreateClient();
    }

    private async Task<string> GetAsAsync(string user)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/users/me");
        request.Headers.Add(UserHeader, user);

        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [Test]
    public async Task TwoAuthenticatedUsers_EachReceiveTheirOwnResponse()
    {
        var first = await GetAsAsync("alice");
        var second = await GetAsAsync("bob");

        Assert.That(first, Does.Contain("alice"));
        Assert.That(second, Does.Contain("bob"), "bob must not be served alice's cached response");
    }

    [Test]
    public async Task TheSameUserTwice_IsServedTheSameResponse()
    {
        var first = await GetAsAsync("alice");
        var second = await GetAsAsync("alice");

        Assert.That(second, Is.EqualTo(first));
    }

    [TearDown]
    public async Task TearDown()
    {
        var cacheFactory = App.Services.GetRequiredService<IActionCacheFactory>();
        var cache = cacheFactory.Create("VaryByUser");
        await cache!.RemoveAsync();
        await App.StopAsync();
    }
}

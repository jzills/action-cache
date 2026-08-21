using System.Security.Claims;
using ActionCache.Common.Keys;
using ActionCache.Common.Keys.VaryBy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Unit.Common.Keys;

[TestFixture]
public class VaryByTests
{
    private ActionCacheVaryByResolver _resolver = null!;

    [SetUp]
    public void SetUp() =>
        _resolver = new ActionCacheVaryByResolver([], NullLogger<ActionCacheVaryByResolver>.Instance);

    private static HttpContext Authenticated(string userId, params Claim[] extraClaims)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        claims.AddRange(extraClaims);

        // The authenticationType argument is what makes IsAuthenticated true.
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "TestAuth"))
        };
    }

    private static HttpContext Anonymous() => new DefaultHttpContext();

    private async Task<string> BuildKeyAsync(HttpContext httpContext, VaryByOptions options)
    {
        var varyBy = await _resolver.ResolveAsync(httpContext, options, CancellationToken.None);
        return new ActionCacheKeyBuilder()
            .WithRouteValues(new Microsoft.AspNetCore.Routing.RouteValueDictionary
            {
                { "controller", "Account" },
                { "action", "Get" }
            })
            .WithVaryByValues(varyBy)
            .Build();
    }

    [Test]
    public async Task Auto_WhenDifferentUsersAreAuthenticated_ProducesDifferentKeys()
    {
        // The security regression: without vary-by these two keys are identical, so the
        // second user is served the first user's response.
        var options = new VaryByOptions();

        var first = await BuildKeyAsync(Authenticated("user-1"), options);
        var second = await BuildKeyAsync(Authenticated("user-2"), options);

        first.Should().NotBe(second);
    }

    [Test]
    public async Task Auto_WhenTheSameUserRequestsTwice_ProducesTheSameKey()
    {
        var options = new VaryByOptions();

        var first = await BuildKeyAsync(Authenticated("user-1"), options);
        var second = await BuildKeyAsync(Authenticated("user-1"), options);

        first.Should().Be(second);
    }

    [Test]
    public async Task Auto_WhenAnonymous_ContributesNoUserValue()
    {
        var varyBy = await _resolver.ResolveAsync(Anonymous(), new VaryByOptions(), CancellationToken.None);

        varyBy.Should().NotContainKey("user");
    }

    [Test]
    public async Task Never_WhenAuthenticated_ProducesOneSharedKey()
    {
        var options = new VaryByOptions { User = VaryByUserMode.Never };

        var first = await BuildKeyAsync(Authenticated("user-1"), options);
        var second = await BuildKeyAsync(Authenticated("user-2"), options);

        first.Should().Be(second);
    }

    [Test]
    public async Task Always_WhenAnonymous_ContributesAStableMarker()
    {
        var options = new VaryByOptions { User = VaryByUserMode.Always };

        var first = await BuildKeyAsync(Anonymous(), options);
        var second = await BuildKeyAsync(Anonymous(), options);

        first.Should().Be(second);

        var varyBy = await _resolver.ResolveAsync(Anonymous(), options, CancellationToken.None);
        varyBy.Should().ContainKey("user");
    }

    [Test]
    public async Task VaryByHeader_ChangesTheKey()
    {
        var options = new VaryByOptions { Headers = "Accept-Language" };

        var english = Anonymous();
        english.Request.Headers["Accept-Language"] = "en";
        var french = Anonymous();
        french.Request.Headers["Accept-Language"] = "fr";

        var first = await BuildKeyAsync(english, options);
        var second = await BuildKeyAsync(french, options);

        first.Should().NotBe(second);
    }

    [Test]
    public async Task VaryByHeader_WhenTheHeaderIsAbsent_DoesNotThrow()
    {
        var options = new VaryByOptions { Headers = "Accept-Language" };

        var act = async () => await BuildKeyAsync(Anonymous(), options);

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task VaryByQuery_ChangesTheKey()
    {
        var options = new VaryByOptions { Query = "page" };

        var first = Anonymous();
        first.Request.QueryString = new QueryString("?page=1");
        var second = Anonymous();
        second.Request.QueryString = new QueryString("?page=2");

        (await BuildKeyAsync(first, options)).Should().NotBe(await BuildKeyAsync(second, options));
    }

    [Test]
    public async Task VaryByClaim_ChangesTheKey()
    {
        var options = new VaryByOptions { User = VaryByUserMode.Never, Claims = "tenant" };

        var first = Authenticated("user-1", new Claim("tenant", "acme"));
        var second = Authenticated("user-1", new Claim("tenant", "globex"));

        (await BuildKeyAsync(first, options)).Should().NotBe(await BuildKeyAsync(second, options));
    }

    [Test]
    public async Task Contributors_RunningInEitherOrder_ProduceTheSameKey()
    {
        var alpha = new StubContributor("alpha", "1");
        var beta = new StubContributor("beta", "2");

        var forward = new ActionCacheVaryByResolver([alpha, beta], NullLogger<ActionCacheVaryByResolver>.Instance);
        var reverse = new ActionCacheVaryByResolver([beta, alpha], NullLogger<ActionCacheVaryByResolver>.Instance);

        var a = await forward.ResolveAsync(Anonymous(), new VaryByOptions(), CancellationToken.None);
        var b = await reverse.ResolveAsync(Anonymous(), new VaryByOptions(), CancellationToken.None);

        new ActionCacheKeyBuilder().WithVaryByValues(a).Build()
            .Should().Be(new ActionCacheKeyBuilder().WithVaryByValues(b).Build());
    }

    [Test]
    public async Task VaryByValues_RoundTripThroughTheKeyComponentsBuilder()
    {
        var varyBy = await _resolver.ResolveAsync(Authenticated("user-1"), new VaryByOptions(), CancellationToken.None);
        var key = new ActionCacheKeyBuilder()
            .WithRouteValues(new Microsoft.AspNetCore.Routing.RouteValueDictionary { { "controller", "Account" } })
            .WithVaryByValues(varyBy)
            .Build();

        var components = new ActionCacheKeyComponentsBuilder(key).Build();

        components.VaryByValues.Should().NotBeNull();
        components.VaryByValues!["user"].Should().Be("user-1");
    }

    private sealed class StubContributor : IActionCacheKeyContributor
    {
        private readonly string _key;
        private readonly string _value;

        public StubContributor(string key, string value)
        {
            _key = key;
            _value = value;
        }

        public ValueTask ContributeAsync(
            HttpContext httpContext,
            IDictionary<string, string?> varyByValues,
            CancellationToken cancellationToken)
        {
            varyByValues[_key] = _value;
            return ValueTask.CompletedTask;
        }
    }
}

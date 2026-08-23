using System.Security.Claims;
using ActionCache;
using ActionCache.Common.Keys.VaryBy;
using ActionCache.Common.Responses;
using ActionCache.Filters;
using ActionCache.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Unit.TestUtilities.Builders;

namespace Unit.Common.Filters;

[TestFixture]
public class ActionCacheFilterVaryByTests
{
    private TemplateBinderFactory _binderFactory = null!;

    [SetUp]
    public void SetUp() =>
        _binderFactory = new ServiceCollection()
            .AddLogging()
            .AddRouting()
            .BuildServiceProvider()
            .GetRequiredService<TemplateBinderFactory>();

    private sealed class StubCache : IActionCache
    {
        private readonly Dictionary<string, object?> _entries = [];

        public Namespace GetNamespace() => new("Account");

        public Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_entries.TryGetValue(key, out var value) ? (TValue?)value : default);

        public Task SetAsync<TValue>(string key, TValue? value, CancellationToken cancellationToken = default)
        {
            _entries[key] = value;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetKeysAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_entries.Keys.AsEnumerable());

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private ActionCacheFilter CreateFilter(IActionCache cache, VaryByOptions options) =>
        new(cache, _binderFactory, NullLogger.Instance,
            SingleFlightBuilder.Build(), false, VaryByBuilder.Resolver(), options, ResponseFactoryBuilder.Build());

    private static ActionExecutingContext ContextFor(string? userId)
    {
        var httpContext = new DefaultHttpContext();
        if (userId is not null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)], authenticationType: "TestAuth"));
        }

        var routeData = new RouteData(new RouteValueDictionary
        {
            { "controller", "Account" },
            { "action", "Get" }
        });

        return new ActionExecutingContext(
            new ActionContext(httpContext, routeData, new ActionDescriptor()),
            [], new Dictionary<string, object?>(), new object());
    }

    private static async Task<IActionResult?> InvokeAsync(
        ActionCacheFilter filter, ActionExecutingContext context, string responseFor)
    {
        await filter.OnActionExecutionAsync(context, () =>
            Task.FromResult(new ActionExecutedContext(context, [], new object())
            {
                Result = new OkObjectResult(responseFor)
            }));

        return context.Result;
    }

    [Test]
    public async Task TwoAuthenticatedUsers_DoNotShareACachedResponse()
    {
        // The security regression: with a shared key the second user short-circuits on the
        // first user's cached entry and never reaches the action at all.
        var cache = new StubCache();
        var options = new VaryByOptions();

        await InvokeAsync(CreateFilter(cache, options), ContextFor("user-1"), "response-for-user-1");

        var secondContext = ContextFor("user-2");
        var executedForSecondUser = false;
        await CreateFilter(cache, options).OnActionExecutionAsync(secondContext, () =>
        {
            executedForSecondUser = true;
            return Task.FromResult(new ActionExecutedContext(secondContext, [], new object())
            {
                Result = new OkObjectResult("response-for-user-2")
            });
        });

        executedForSecondUser.Should().BeTrue(
            "a second user must not be served the first user's cached response");
        secondContext.Result.Should().BeNull("the action ran, so the result comes from the pipeline");
    }

    [Test]
    public async Task TheSameUserTwice_IsServedFromCache()
    {
        var cache = new StubCache();
        var options = new VaryByOptions();

        await InvokeAsync(CreateFilter(cache, options), ContextFor("user-1"), "first-response");

        var secondContext = ContextFor("user-1");
        var executed = false;
        await CreateFilter(cache, options).OnActionExecutionAsync(secondContext, () =>
        {
            executed = true;
            return Task.FromResult(new ActionExecutedContext(secondContext, [], new object()));
        });

        executed.Should().BeFalse("the same user must hit their own cached entry");
        (secondContext.Result as ContentResult)!.Content.Should().Be("\"first-response\"");
    }

    [Test]
    public async Task VaryByUserNever_LetsTwoUsersShareOneEntry()
    {
        var cache = new StubCache();
        var options = new VaryByOptions { User = VaryByUserMode.Never };

        await InvokeAsync(CreateFilter(cache, options), ContextFor("user-1"), "shared-response");

        var secondContext = ContextFor("user-2");
        var executed = false;
        await CreateFilter(cache, options).OnActionExecutionAsync(secondContext, () =>
        {
            executed = true;
            return Task.FromResult(new ActionExecutedContext(secondContext, [], new object()));
        });

        executed.Should().BeFalse("Never opts back in to one shared entry");
        (secondContext.Result as ContentResult)!.Content.Should().Be("\"shared-response\"");
    }

    [Test]
    public async Task AnonymousRequests_StillShareOneEntry()
    {
        var cache = new StubCache();
        var options = new VaryByOptions();

        await InvokeAsync(CreateFilter(cache, options), ContextFor(null), "anonymous-response");

        var secondContext = ContextFor(null);
        var executed = false;
        await CreateFilter(cache, options).OnActionExecutionAsync(secondContext, () =>
        {
            executed = true;
            return Task.FromResult(new ActionExecutedContext(secondContext, [], new object()));
        });

        executed.Should().BeFalse("anonymous callers have no identity to separate");
        (secondContext.Result as ContentResult)!.Content.Should().Be("\"anonymous-response\"");
    }
}

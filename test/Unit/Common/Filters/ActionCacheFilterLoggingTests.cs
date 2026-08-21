using Unit.TestUtilities.Builders;
using ActionCache;
using ActionCache.Filters;
using ActionCache.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Unit.TestUtilities;

namespace Unit.Common.Filters;

/// <summary>
/// Locks in the filter-level diagnostics: the events fired for conditions only the filter
/// can observe (no cache key, non-cacheable result) and the absence of duplicate hit/set
/// logging, which belongs to <c>ResilientActionCache</c>.
/// </summary>
[TestFixture]
public class ActionCacheFilterLoggingTests
{
    private Mock<IActionCache> _cacheMock;
    private CapturingLogger _logger;
    private ActionCacheFilter _sut;

    [SetUp]
    public void SetUp()
    {
        _cacheMock = new Mock<IActionCache>();
        _cacheMock.Setup(cache => cache.GetNamespace()).Returns(new Namespace("Test"));

        var binderFactory = new ServiceCollection()
            .AddLogging()
            .AddRouting()
            .BuildServiceProvider()
            .GetRequiredService<TemplateBinderFactory>();

        _logger = new CapturingLogger();
        _sut = new ActionCacheFilter(_cacheMock.Object, binderFactory, _logger, SingleFlightBuilder.Build(), true, VaryByBuilder.Resolver(), VaryByBuilder.Options());
    }

    [Test]
    public void LogCacheKeyUnavailable_LogsFilterNameAndNamespace()
    {
        // The no-key branch is unreachable through the real key builder (it never
        // produces an empty key), so the helper is exercised directly.
        var binderFactory = new ServiceCollection()
            .AddLogging()
            .AddRouting()
            .BuildServiceProvider()
            .GetRequiredService<TemplateBinderFactory>();
        var filter = new KeyUnavailableProbeFilter(_cacheMock.Object, binderFactory, _logger);

        filter.KeyUnavailable();

        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(2000);
        entry.Level.Should().Be(LogLevel.Debug);
        entry.Message.Should().Contain(nameof(KeyUnavailableProbeFilter)).And.Contain("Test");
    }

    private sealed class KeyUnavailableProbeFilter : ActionCacheFilterBase
    {
        public KeyUnavailableProbeFilter(IActionCache cache, TemplateBinderFactory binderFactory, ILogger logger)
            : base(cache, binderFactory, logger)
        {
        }

        public void KeyUnavailable() => LogCacheKeyUnavailable();
    }

    [Test]
    public async Task OnActionExecutionAsync_WhenResultNotCacheable_LogsResultNotCacheable()
    {
        _cacheMock.Setup(cache => cache.GetAsync<IActionResult?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IActionResult?)null);

        var context = BuildActionExecutingContext();
        ActionExecutionDelegate next = () =>
        {
            var executed = new ActionExecutedContext(context, [], new object())
            {
                Result = new NotFoundObjectResult("missing")
            };
            return Task.FromResult(executed);
        };

        await _sut.OnActionExecutionAsync(context, next);

        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(2001);
        entry.Level.Should().Be(LogLevel.Debug);
        entry.Message.Should().Contain("not a cacheable success result");
    }

    [Test]
    public async Task OnActionExecutionAsync_WhenCacheHit_LogsNothingAtFilterLevel()
    {
        _cacheMock.Setup(cache => cache.GetAsync<IActionResult?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OkObjectResult("cached"));

        var context = BuildActionExecutingContext();
        ActionExecutionDelegate next = () =>
            Task.FromResult(new ActionExecutedContext(context, [], new object()));

        await _sut.OnActionExecutionAsync(context, next);

        _logger.Entries.Should().BeEmpty();
    }

    [Test]
    public async Task OnActionExecutionAsync_WhenResultIsCached_LogsNothingAtFilterLevel()
    {
        _cacheMock.Setup(cache => cache.GetAsync<IActionResult?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IActionResult?)null);

        var context = BuildActionExecutingContext();
        ActionExecutionDelegate next = () =>
        {
            var executed = new ActionExecutedContext(context, [], new object())
            {
                Result = new OkObjectResult("fresh")
            };
            return Task.FromResult(executed);
        };

        await _sut.OnActionExecutionAsync(context, next);

        _logger.Entries.Should().BeEmpty();
    }

    private static ActionExecutingContext BuildActionExecutingContext(
        RouteValueDictionary? routeValues = null)
    {
        routeValues ??= new RouteValueDictionary
        {
            { "area", "area" },
            { "controller", "controller" },
            { "action", "action" }
        };

        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData(routeValues);
        var actionDescriptor = new ActionDescriptor();
        var actionContext = new ActionContext(httpContext, routeData, actionDescriptor);

        return new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object());
    }
}

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
using Moq;

namespace Unit.Common.Filters;

[TestFixture]
public class ActionCacheRefreshFilterTests
{
    private Mock<IActionCache> _cacheMock;
    private TemplateBinderFactory _binderFactory;
    private ActionCacheRefreshFilter _sut;

    [SetUp]
    public void SetUp()
    {
        _cacheMock = new Mock<IActionCache>();
        _cacheMock.Setup(cache => cache.GetNamespace()).Returns(new Namespace("Test"));

        _binderFactory = new ServiceCollection()
            .AddLogging()
            .AddRouting()
            .BuildServiceProvider()
            .GetRequiredService<TemplateBinderFactory>();

        _sut = new ActionCacheRefreshFilter(_cacheMock.Object, _binderFactory);
    }

    [Test]
    public async Task OnResultExecutionAsync_WhenOkObjectResult_CallsRefreshAsync()
    {
        var context = BuildResultExecutingContext(new OkObjectResult("ok"));
        ResultExecutionDelegate next = () => Task.FromResult(BuildResultExecutedContext(context));

        await _sut.OnResultExecutionAsync(context, next);

        _cacheMock.Verify(cache => cache.RefreshAsync(), Times.Once);
    }

    [Test]
    public async Task OnResultExecutionAsync_WhenBadRequestResult_DoesNotCallRefreshAsync()
    {
        var context = BuildResultExecutingContext(new BadRequestResult());
        ResultExecutionDelegate next = () => Task.FromResult(BuildResultExecutedContext(context));

        await _sut.OnResultExecutionAsync(context, next);

        _cacheMock.Verify(cache => cache.RefreshAsync(), Times.Never);
    }

    [Test]
    public async Task OnResultExecutionAsync_Always_CallsNext()
    {
        var context = BuildResultExecutingContext(new OkResult());
        var nextCalled = false;
        ResultExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(BuildResultExecutedContext(context));
        };

        await _sut.OnResultExecutionAsync(context, next);

        nextCalled.Should().BeTrue();
    }

    [Test]
    public async Task OnResultExecutionAsync_WhenStatusCodeResult200_CallsRefreshAsync()
    {
        var context = BuildResultExecutingContext(new StatusCodeResult(200));
        ResultExecutionDelegate next = () => Task.FromResult(BuildResultExecutedContext(context));

        await _sut.OnResultExecutionAsync(context, next);

        _cacheMock.Verify(cache => cache.RefreshAsync(), Times.Once);
    }

    private static ResultExecutingContext BuildResultExecutingContext(IActionResult result)
    {
        var actionContext = BuildActionContext();
        return new ResultExecutingContext(actionContext, [], result, new object());
    }

    private static ResultExecutedContext BuildResultExecutedContext(ResultExecutingContext ctx) =>
        new ResultExecutedContext(
            new ActionContext(ctx.HttpContext, ctx.RouteData, ctx.ActionDescriptor),
            [],
            ctx.Result,
            new object());

    private static ActionContext BuildActionContext() =>
        new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
}

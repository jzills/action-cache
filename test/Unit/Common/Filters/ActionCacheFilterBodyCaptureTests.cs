using ActionCache.Common.Responses;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Unit.TestUtilities.Builders;

namespace Unit.Common.Filters;

/// <summary>
/// What a cache entry records of the request that produced it.
/// </summary>
[TestFixture]
public class ActionCacheFilterBodyCaptureTests
{
    private sealed record Payload(string Name);

    private static ActionExecutingContext ContextWithBody(Payload payload)
    {
        var descriptor = new ControllerActionDescriptor
        {
            Parameters =
            [
                new ControllerParameterDescriptor
                {
                    Name = "payload",
                    ParameterType = typeof(Payload),
                    BindingInfo = new BindingInfo { BindingSource = BindingSource.Body }
                }
            ]
        };

        return new ActionExecutingContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), descriptor),
            [],
            new Dictionary<string, object?> { ["payload"] = payload },
            controller: null!);
    }

    private static object? Invoke(ActionExecutingContext context, bool variesByRequest) =>
        typeof(ActionCache.Filters.ActionCacheFilter)
            .GetMethod("GetBoundBody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [context, variesByRequest]);

    [Test]
    public void GetBoundBody_ForAnEntryThatDoesNotVaryByRequest_RecordsThePayload()
    {
        var payload = new Payload("Ada");

        var captured = Invoke(ContextWithBody(payload), variesByRequest: false);

        captured.Should().BeSameAs(payload, "refresh replays this request, so it needs the body");
    }

    [Test]
    public void GetBoundBody_ForAnEntryThatVariesByRequest_RecordsNothing()
    {
        // Refresh skips vary-by entries outright, so the payload could never be replayed.
        // Storing it would put request bodies in Redis, SQL Server or Cosmos for nothing —
        // and since VaryByUserMode.Auto that is every authenticated endpoint.
        var captured = Invoke(ContextWithBody(new Payload("Ada")), variesByRequest: true);

        captured.Should().BeNull();
    }

    [Test]
    public void CreateRequest_WithNoBody_RecordsNeitherBodyNorContentType()
    {
        var factory = ResponseFactoryBuilder.Build();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/users";

        var request = factory.CreateRequest(httpContext);

        request!.Body.Should().BeNull();
        request.ContentType.Should().BeNull();
    }
}

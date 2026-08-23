using System.Reflection;
using ActionCache.Filters;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace Unit.Common.EndpointFilters;

/// <summary>
/// A Minimal API endpoint records the body it was called with, the same as an MVC action.
/// </summary>
/// <remarks>
/// It did not: the MVC filter reads the body parameter off the action descriptor, and the
/// endpoint filter simply passed none. A cached <c>MapPost</c> therefore stored
/// <c>Body = null</c>, its replay went out bodyless, the endpoint answered 400 or 415, and
/// refresh could never replace the entry — a permanent no-op for every Minimal API that
/// takes a payload.
/// </remarks>
[TestFixture]
public class ActionCacheEndpointFilterBodyCaptureTests
{
    private sealed record Payload(string Name);

    private sealed class AcceptsPayload : IAcceptsMetadata
    {
        public IReadOnlyList<string> ContentTypes => ["application/json"];
        public Type? RequestType => typeof(Payload);
        public bool IsOptional => false;
    }

    private static EndpointFilterInvocationContext BuildContext(object?[] arguments, bool acceptsBody)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            acceptsBody ? new EndpointMetadataCollection(new AcceptsPayload()) : EndpointMetadataCollection.Empty,
            "Test"));

        var contextMock = new Mock<EndpointFilterInvocationContext>();
        contextMock.Setup(context => context.HttpContext).Returns(httpContext);
        contextMock.Setup(context => context.Arguments).Returns(arguments);
        return contextMock.Object;
    }

    private static object? Invoke(EndpointFilterInvocationContext context, bool variesByRequest) =>
        typeof(ActionCacheEndpointFilter)
            .GetMethod("GetBoundBody", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [context, variesByRequest]);

    [Test]
    public void GetBoundBody_ForAnEndpointThatAcceptsABody_RecordsTheMatchingArgument()
    {
        var payload = new Payload("Ada");

        // The route value argument is not the body; IAcceptsMetadata.RequestType is what
        // distinguishes them.
        var captured = Invoke(BuildContext([42, payload], acceptsBody: true), variesByRequest: false);

        captured.Should().BeSameAs(payload);
    }

    [Test]
    public void GetBoundBody_ForAnEndpointWithNoBody_RecordsNothing()
    {
        var captured = Invoke(BuildContext([42], acceptsBody: false), variesByRequest: false);

        captured.Should().BeNull();
    }

    [Test]
    public void GetBoundBody_ForAnEntryThatVariesByRequest_RecordsNothing()
    {
        var captured = Invoke(BuildContext([new Payload("Ada")], acceptsBody: true), variesByRequest: true);

        captured.Should().BeNull("refresh skips vary-by entries, so the payload could never be replayed");
    }
}

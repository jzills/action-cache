using System.Reflection;
using Microsoft.AspNetCore.Mvc;
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

    private sealed class Accepts(Type requestType) : IAcceptsMetadata
    {
        public IReadOnlyList<string> ContentTypes => ["application/json"];
        public Type? RequestType { get; } = requestType;
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

    /// <summary>
    /// Builds a context the way Minimal APIs do: the handler's <see cref="MethodInfo"/> in
    /// endpoint metadata, arguments positional against its parameters, and the route values
    /// the request matched.
    /// </summary>
    private static EndpointFilterInvocationContext BuildContext(
        MethodInfo handler,
        Type requestType,
        object?[] arguments,
        params string[] routeTokens)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new Accepts(requestType), handler),
            "Test"));

        foreach (var token in routeTokens)
        {
            httpContext.Request.RouteValues[token] = "route-value";
        }

        var contextMock = new Mock<EndpointFilterInvocationContext>();
        contextMock.Setup(context => context.HttpContext).Returns(httpContext);
        contextMock.Setup(context => context.Arguments).Returns(arguments);
        return contextMock.Object;
    }

    private static MethodInfo Handler(string name) =>
        typeof(Handlers).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    private static class Handlers
    {
        // MapPost("/echo/{name}", (string name, [FromBody] string payload) => ...)
        internal static string RouteValueSharesBodyType(string name, [FromBody] string payload) => payload;

        // MapPost("/echo/{name}", (string name, string payload) => ...)
        internal static string UnattributedRouteToken(string name, string payload) => payload;

        // Two equally plausible candidates and nothing to separate them.
        internal static string Ambiguous(string first, string second) => second;

        // MapPost("/obj/{id}", (int id, Payload payload) => ...)
        internal static Payload ComplexBody(int id, Payload payload) => payload;
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

    [Test]
    public void GetBoundBody_WhenARouteValueSharesTheBodyType_RecordsTheBodyNotTheRouteValue()
    {
        // IAcceptsMetadata says the body is a string, and so is the {name} route value.
        // Matching on type alone picked the route value, and refresh then replayed that as
        // the payload — overwriting the entry with a response for different input.
        var context = BuildContext(
            Handler(nameof(Handlers.RouteValueSharesBodyType)),
            typeof(string),
            ["ada", "the-payload"],
            "name");

        Invoke(context, variesByRequest: false).Should().Be("the-payload");
    }

    [Test]
    public void GetBoundBody_WhenAnUnattributedParameterIsNamedAfterARouteToken_RecordsTheOtherOne()
    {
        // No [FromBody] to settle it: the parameter named after a route token binds from the
        // route, which leaves exactly one candidate for the body.
        var context = BuildContext(
            Handler(nameof(Handlers.UnattributedRouteToken)),
            typeof(string),
            ["ada", "the-payload"],
            "name");

        Invoke(context, variesByRequest: false).Should().Be("the-payload");
    }

    [Test]
    public void GetBoundBody_WhenTheBodyParameterIsAmbiguous_RecordsNothing()
    {
        var context = BuildContext(
            Handler(nameof(Handlers.Ambiguous)),
            typeof(string),
            ["one", "two"]);

        Invoke(context, variesByRequest: false).Should().BeNull(
            "a wrong body corrupts the entry on replay, a missing one only leaves it stale");
    }

    [Test]
    public void GetBoundBody_ForAComplexBodyAlongsideARouteValue_RecordsTheBody()
    {
        var payload = new Payload("Ada");
        var context = BuildContext(
            Handler(nameof(Handlers.ComplexBody)),
            typeof(Payload),
            [42, payload],
            "id");

        Invoke(context, variesByRequest: false).Should().BeSameAs(payload);
    }

    [Test]
    public void GetBoundBody_WhenArgumentsDoNotLineUpWithTheHandler_FallsBackToAUniqueTypeMatch()
    {
        var payload = new Payload("Ada");

        // Arity disagrees with the handler, so the positional mapping cannot be trusted;
        // exactly one argument is of the accepted type, which is unambiguous on its own.
        var context = BuildContext(
            Handler(nameof(Handlers.ComplexBody)),
            typeof(Payload),
            [1, 2, payload]);

        Invoke(context, variesByRequest: false).Should().BeSameAs(payload);
    }
}

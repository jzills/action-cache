using ActionCache.Common.Extensions.Internal;
using ActionCache.Utilities;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;

namespace Unit.Common.Extensions;

[TestFixture]
public class NamespaceExtensionsTests
{
    private TemplateBinderFactory _binderFactory;

    [SetUp]
    public void SetUp()
    {
        _binderFactory = new ServiceCollection()
            .AddLogging()
            .AddRouting()
            .BuildServiceProvider()
            .GetRequiredService<TemplateBinderFactory>();
    }

    [Test]
    public void ContainsRouteTemplateParameters_WhenNamespaceHasTemplate_ReturnsTrue()
    {
        var @namespace = new Namespace("Account:{id}");

        var result = @namespace.ContainsRouteTemplateParameters();

        result.Should().BeTrue();
    }

    [Test]
    public void ContainsRouteTemplateParameters_WhenNamespaceHasNoTemplate_ReturnsFalse()
    {
        var @namespace = new Namespace("Account");

        var result = @namespace.ContainsRouteTemplateParameters();

        result.Should().BeFalse();
    }

    [Test]
    public void AttachRouteValues_WhenNamespaceHasTemplate_SetsValueWithRouteTemplateParameters()
    {
        var @namespace = new Namespace("{controller}");
        var routeValues = new RouteValueDictionary
        {
            { "controller", "users" }
        };

        @namespace.AttachRouteValues(routeValues, _binderFactory);

        @namespace.ValueWithRouteTemplateParameters.Should().NotBeNull();
        @namespace.ValueWithRouteTemplateParameters.Should().Contain("users");
    }

    [Test]
    public void AttachRouteValues_WhenNamespaceHasNoTemplate_DoesNotSetValueWithRouteTemplateParameters()
    {
        var @namespace = new Namespace("Account");
        var routeValues = new RouteValueDictionary
        {
            { "controller", "users" }
        };

        @namespace.AttachRouteValues(routeValues, _binderFactory);

        @namespace.ValueWithRouteTemplateParameters.Should().BeNull();
    }
}

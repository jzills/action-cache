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

    [Test]
    public void AttachRouteValues_WhenRouteValueContainsSeparator_EscapesValueButKeepsTemplateSeparator()
    {
        var @namespace = new Namespace("Users:{id}");
        var routeValues = new RouteValueDictionary
        {
            { "id", "a:b:c" }
        };

        @namespace.AttachRouteValues(routeValues, _binderFactory);

        // The template's own separator remains a raw ':', but the ':' characters
        // from the user-supplied value are escaped so they cannot act as extra
        // namespace separators (which would enable cross-namespace collision /
        // eviction of another resource).
        @namespace.ValueWithRouteTemplateParameters.Should().NotBeNull();
        @namespace.ValueWithRouteTemplateParameters.Should().StartWith("Users:");
        @namespace.ValueWithRouteTemplateParameters!["Users:".Length..].Should().NotContain(":");
    }

    [Test]
    public void AttachRouteValues_WhenValueIsClean_ResolvedNamespaceMatchesEquivalentLiteralNamespace()
    {
        // A templated namespace resolved with a clean value must produce the same
        // fully-qualified namespace as the equivalent literal (non-templated) one,
        // so factory.Create("Teams:<id>") locates entries cached under
        // [ActionCache(Namespace="Teams:{id}")]. Regression guard for the escaping fix.
        var templated = new Namespace("Teams:{id}");
        templated.AttachRouteValues(new RouteValueDictionary { { "id", "42" } }, _binderFactory);

        Namespace literal = "Teams:42";

        ((string)templated).Should().Be((string)literal);
    }
}

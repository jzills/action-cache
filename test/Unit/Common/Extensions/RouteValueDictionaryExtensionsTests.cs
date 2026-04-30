using ActionCache.Common.Extensions.Internal;
using Microsoft.AspNetCore.Routing;

namespace Unit.Common.Extensions;

[TestFixture]
public class RouteValueDictionaryExtensionsTests
{
    [Test]
    public void TryGetStringValue_WhenKeyExistsWithValue_ReturnsTrueAndValue()
    {
        var dict = new RouteValueDictionary { { "controller", "home" } };

        var found = dict.TryGetStringValue("controller", out var value);

        found.Should().BeTrue();
        value.Should().Be("home");
    }

    [Test]
    public void TryGetStringValue_WhenKeyDoesNotExist_ReturnsFalseAndNull()
    {
        var dict = new RouteValueDictionary();

        var found = dict.TryGetStringValue("missing", out var value);

        found.Should().BeFalse();
        value.Should().BeNull();
    }

    [Test]
    public void TryGetStringValue_WhenKeyExistsWithNullValue_ReturnsFalse()
    {
        var dict = new RouteValueDictionary { { "area", null } };

        var found = dict.TryGetStringValue("area", out var value);

        found.Should().BeFalse();
        value.Should().BeNull();
    }

    [Test]
    public void TryGetStringValue_IsCaseInsensitive()
    {
        var dict = new RouteValueDictionary { { "Controller", "home" } };

        var found = dict.TryGetStringValue("controller", out var value);

        found.Should().BeTrue();
        value.Should().Be("home");
    }
}

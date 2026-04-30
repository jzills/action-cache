using ActionCache.Attributes;
using ActionCache.Common.Extensions.Internal;
using System.Reflection;

namespace Unit.Common.Extensions;

[TestFixture]
public class MethodInfoExtensionsTests
{
    [Test]
    public void TryGetRefreshResult_WhenMethodReturnsValue_ReturnsTrueWithValue()
    {
        var instance = new SampleController();
        var methodInfo = typeof(SampleController).GetMethod(nameof(SampleController.GetValue))!;

        var success = methodInfo.TryGetRefreshResult(instance, [], out var value);

        success.Should().BeTrue();
        value.Should().Be("result");
    }

    [Test]
    public void TryGetRefreshResult_WhenMethodReturnsNull_ReturnsFalse()
    {
        var instance = new SampleController();
        var methodInfo = typeof(SampleController).GetMethod(nameof(SampleController.GetNull))!;

        var success = methodInfo.TryGetRefreshResult(instance, [], out var value);

        success.Should().BeFalse();
        value.Should().BeNull();
    }

    [Test]
    public void TryGetRefreshResult_WhenMethodInfoIsNull_ReturnsFalse()
    {
        MethodInfo? methodInfo = null;

        var success = methodInfo.TryGetRefreshResult(new object(), [], out var value);

        success.Should().BeFalse();
        value.Should().BeNull();
    }

    [Test]
    public void HasActionCacheAttribute_WhenMethodHasMatchingAttribute_ReturnsTrue()
    {
        var methodInfo = typeof(SampleController).GetMethod(nameof(SampleController.CachedAction))!;
        var fullNamespace = $"ActionCache:{SampleController.CachedNamespace}";

        var result = methodInfo.HasActionCacheAttribute(fullNamespace);

        result.Should().BeTrue();
    }

    [Test]
    public void HasActionCacheAttribute_WhenMethodHasNonMatchingAttribute_ReturnsFalse()
    {
        var methodInfo = typeof(SampleController).GetMethod(nameof(SampleController.CachedAction))!;

        var result = methodInfo.HasActionCacheAttribute("ActionCache:OtherNamespace");

        result.Should().BeFalse();
    }

    [Test]
    public void HasActionCacheAttribute_WhenMethodHasNoAttribute_ReturnsFalse()
    {
        var methodInfo = typeof(SampleController).GetMethod(nameof(SampleController.GetValue))!;

        var result = methodInfo.HasActionCacheAttribute("ActionCache:Test");

        result.Should().BeFalse();
    }
}

file class SampleController
{
    public const string CachedNamespace = "TestNs";

    public string GetValue() => "result";

    public string? GetNull() => null;

    [ActionCache(Namespace = CachedNamespace)]
    public string CachedAction() => "cached";
}

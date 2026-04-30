using ActionCache.Redis;
using ActionCache.Redis.Extensions;
using System.Reflection;

namespace Unit.Redis;

[TestFixture]
public class AssemblyExtensionsTests
{
    private Assembly _assembly;

    [SetUp]
    public void SetUp() => _assembly = typeof(RedisActionCache).Assembly;

    [Test]
    public void TryGetResourceAsText_WhenResourceExists_ReturnsTrueWithContent()
    {
        var result = _assembly.TryGetResourceAsText(LuaResources.SetHash, out var text);

        result.Should().BeTrue();
        text.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public void TryGetResourceAsText_WhenResourceDoesNotExist_ReturnsFalse()
    {
        var result = _assembly.TryGetResourceAsText("nonexistent.lua", out var text);

        result.Should().BeFalse();
        text.Should().BeNullOrEmpty();
    }

    [Test]
    public void TryGetResourceAsText_CalledTwice_ReturnsCachedResult()
    {
        _assembly.TryGetResourceAsText(LuaResources.Remove, out var firstText);
        var result = _assembly.TryGetResourceAsText(LuaResources.Remove, out var secondText);

        result.Should().BeTrue();
        secondText.Should().Be(firstText);
    }

    [Test]
    public void GetResourceName_WhenResourceExists_ReturnsFullResourceName()
    {
        var result = _assembly.GetResourceName(LuaResources.SetHash);

        result.Should().NotBeNullOrWhiteSpace();
        result.Should().EndWith(LuaResources.SetHash);
    }

    [Test]
    public void GetResourceName_WhenResourceDoesNotExist_ReturnsEmptyString()
    {
        var result = _assembly.GetResourceName("nonexistent.lua");

        result.Should().BeEmpty();
    }
}

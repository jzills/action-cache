using ActionCache.Common.Extensions.Internal;
using System.Collections.Specialized;

namespace Unit.Common.Extensions;

[TestFixture]
public class NameValueCollectionExtensionsTests
{
    [Test]
    public void ParseValueAsJson_WhenKeyMissing_ReturnsNull()
    {
        var collection = new NameValueCollection();

        var result = collection.ParseValueAsJson<TestModel>("missing");

        result.Should().BeNull();
    }

    [Test]
    public void ParseValueAsJson_WhenKeyHasWhitespaceValue_ReturnsNull()
    {
        var collection = new NameValueCollection { { "key", "   " } };

        var result = collection.ParseValueAsJson<TestModel>("key");

        result.Should().BeNull();
    }

    [Test]
    public void ParseValueAsJson_WhenKeyHasValidJson_ReturnsDeserializedObject()
    {
        var collection = new NameValueCollection { { "key", "{\"Name\":\"Alice\"}" } };

        var result = collection.ParseValueAsJson<TestModel>("key");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Alice");
    }

    [Test]
    public void ParseValueAsJson_WhenKeyHasInvalidJson_ReturnsNull()
    {
        var collection = new NameValueCollection { { "key", "not-valid-json{{{" } };

        var result = collection.ParseValueAsJson<TestModel>("key");

        result.Should().BeNull();
    }
}

file class TestModel
{
    public string? Name { get; set; }
}

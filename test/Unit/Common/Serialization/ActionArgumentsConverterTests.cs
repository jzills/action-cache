using ActionCache.Common.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Unit.Common.Serialization;

[TestFixture]
public class ActionArgumentsConverterTests
{
    private ActionArgumentsConverter _sut;
    private JsonSerializerSettings _settings;

    [SetUp]
    public void SetUp()
    {
        _sut = new ActionArgumentsConverter();
        _settings = new JsonSerializerSettings { Converters = { _sut } };
    }

    private Dictionary<string, object> Deserialize(string json) =>
        JsonConvert.DeserializeObject<Dictionary<string, object>>(json, _settings)!;

    [Test]
    public void ReadJson_WithStringValue_ReturnsString()
    {
        var result = Deserialize(@"{""key"": ""hello""}");

        result["key"].Should().Be("hello");
    }

    [Test]
    public void ReadJson_WithGuidString_ReturnsGuid()
    {
        var guid = Guid.NewGuid();
        var result = Deserialize($@"{{""key"": ""{guid}""}}");

        result["key"].Should().Be(guid);
    }

    [Test]
    public void ReadJson_WithDateTimeString_ReturnsDateTime()
    {
        var result = Deserialize(@"{""key"": ""2024-01-15T10:30:00""}");

        result["key"].Should().BeOfType<DateTime>();
    }

    [Test]
    public void ReadJson_WithIntegerValue_ReturnsLong()
    {
        var result = Deserialize(@"{""key"": 42}");

        result["key"].Should().Be(42L);
    }

    [Test]
    public void ReadJson_WithFloatValue_ReturnsDecimal()
    {
        var result = Deserialize(@"{""key"": 3.14}");

        result["key"].Should().BeOfType<decimal>();
    }

    [Test]
    public void ReadJson_WithBooleanValue_ReturnsBool()
    {
        var result = Deserialize(@"{""key"": true}");

        result["key"].Should().Be(true);
    }

    [Test]
    public void ReadJson_WithNullValue_ReturnsNull()
    {
        var result = Deserialize(@"{""key"": null}");

        result["key"].Should().BeNull();
    }

    [Test]
    public void ReadJson_WithArrayValue_ReturnsObjectArray()
    {
        var result = Deserialize(@"{""key"": [1, 2, 3]}");

        result["key"].Should().BeOfType<object[]>();
    }

    [Test]
    public void ReadJson_WithObjectValue_ReturnsObject()
    {
        var result = Deserialize(@"{""key"": {""nested"": ""value""}}");

        result["key"].Should().NotBeNull();
    }

    [Test]
    public void WriteJson_WithNonNullDictionary_SerializesAllEntries()
    {
        var source = new Dictionary<string, object> { { "a", 1 }, { "b", "two" } };

        var json = JsonConvert.SerializeObject(source, _settings);

        json.Should().Contain("\"a\"");
        json.Should().Contain("\"b\"");
    }

    [Test]
    public void WriteJson_WithNullDictionary_WritesEmptyObject()
    {
        var writer = new StringWriter();
        var jsonWriter = new JsonTextWriter(writer);
        var serializer = JsonSerializer.Create(_settings);

        _sut.WriteJson(jsonWriter, null, serializer);

        writer.ToString().Should().Be("{}");
    }
}

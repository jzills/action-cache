using ActionCache.Common.Keys;

namespace Unit.Common.Keys;

[TestFixture]
public class KeyEncoderTests
{
    private KeyEncoder _sut;

    [SetUp]
    public void SetUp() => _sut = new KeyEncoder();

    [Test]
    public void Encode_Always_ReturnsHexString()
    {
        var result = _sut.Encode("hello");

        result.Should().Be("68656C6C6F");
    }

    [Test]
    public void Decode_WhenValidHex_ReturnsOriginalString()
    {
        var result = _sut.Decode("68656C6C6F");

        result.Should().Be("hello");
    }

    [Test]
    public void Decode_WhenInvalidHex_ThrowsFormatException()
    {
        Action act = () => _sut.Decode("not-valid-hex!");

        act.Should().Throw<FormatException>();
    }

    [Test]
    public void Encode_ThenDecode_ReturnsOriginalString()
    {
        var original = "RouteValuesKey={\"area\":\"admin\"}&ActionArgumentsKey={}";

        var decoded = _sut.Decode(_sut.Encode(original));

        decoded.Should().Be(original);
    }

    [TestCase("")]
    [TestCase("a b c")]
    [TestCase("key=value&other=123")]
    public void Encode_ThenDecode_PreservesArbitraryStrings(string original)
    {
        var decoded = _sut.Decode(_sut.Encode(original));

        decoded.Should().Be(original);
    }
}

using ActionCache.Common.Keys;

namespace Unit.Common.Keys;

[TestFixture]
public class KeyHashGeneratorTests
{
    [Test]
    public void ToHash_WithValue_ReturnsSha256HexString()
    {
        var result = KeyHashGenerator.ToHash("hello");

        result.Should().NotBeNullOrEmpty();
        result.Should().HaveLength(64);
        result.Should().MatchRegex("^[0-9A-F]+$");
    }

    [Test]
    public void ToHash_SameInput_ReturnsSameHash()
    {
        var hash1 = KeyHashGenerator.ToHash("test-key");
        var hash2 = KeyHashGenerator.ToHash("test-key");

        hash1.Should().Be(hash2);
    }

    [Test]
    public void ToHash_DifferentInputs_ReturnsDifferentHashes()
    {
        var hash1 = KeyHashGenerator.ToHash("key1");
        var hash2 = KeyHashGenerator.ToHash("key2");

        hash1.Should().NotBe(hash2);
    }

    [Test]
    public void ToHash_EmptyString_ReturnsHash()
    {
        var result = KeyHashGenerator.ToHash(string.Empty);

        result.Should().NotBeNullOrEmpty();
        result.Should().HaveLength(64);
    }
}

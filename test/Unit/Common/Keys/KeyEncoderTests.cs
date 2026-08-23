using ActionCache.Common.Keys;

namespace Unit.Common.Keys;

[TestFixture]
public class KeyEncoderTests
{
    private KeyEncoder _sut = null!;

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

    // Bug L1: KeyEncoder is misnamed — it performs hex encoding, not AES or any form of
    // encryption. Cache keys containing PII (email, user ID, session token) are stored in
    // the backend as plain hex, offering zero confidentiality. The class name "KeyEncoder"
    // and any references to "encryption" in documentation are misleading.
    //
    // Fix: rename to HexEncoder. If confidentiality is required, implement HMAC-SHA256
    // key derivation (opaque + deterministic) or AES-GCM encryption with a configured key.

    [Test]
    public void Encode_WithSameInput_AlwaysProducesSameOutput_IsNotEncryption_BugL1()
    {
        // True encryption randomises output via an IV — the same plaintext produces different
        // ciphertext on each call. KeyEncoder is deterministic, proving it is not encryption.
        var firstCall = _sut.Encode("sensitive-data");
        var secondCall = _sut.Encode("sensitive-data");

        firstCall.Should().Be(secondCall);
    }

    [Test]
    public void Encode_OutputIsOnlyHexCharacters_IsNotEncryption_BugL1()
    {
        var encoded = _sut.Encode("user_id=42&email=alice@example.com");

        // Output is uppercase hex (A-F, 0-9) — the signature of Convert.ToHexString,
        // not a ciphertext or base64-encoded blob.
        encoded.Should().MatchRegex("^[0-9A-F]+$");
    }

    [Test]
    public void Decode_RequiresNoKey_IsNotEncryption_BugL1()
    {
        var encoded = _sut.Encode("secret-value");

        // Symmetric decryption requires a secret key. KeyEncoder.Decode requires no key at all —
        // anyone with the hex string can recover the original value.
        var decoded = _sut.Decode(encoded);

        decoded.Should().Be("secret-value");
    }
}

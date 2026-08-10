using MedMateAI.Application.Service;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class InvitationTokenServiceTests
{
    private InvitationTokenService _service = null!;

    [SetUp]
    public void SetUp() => _service = new InvitationTokenService();

    // ── GenerateToken ──────────────────────────────────────────────────────────

    [Test]
    [Category("N")]
    public void GenerateToken_ReturnsNonEmptyString()
    {
        var token = _service.GenerateToken();
        Assert.That(token, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    [Category("N")]
    public void GenerateToken_ReturnsBase64UrlEncodedString_NoStandardBase64Padding()
    {
        var token = _service.GenerateToken();
        // Base64Url has no '=' padding and no '+' or '/'
        Assert.That(token, Does.Not.Contain("="));
        Assert.That(token, Does.Not.Contain("+"));
        Assert.That(token, Does.Not.Contain("/"));
    }

    [Test]
    [Category("B")]
    public void GenerateToken_TokenLength_Is43Characters()
    {
        // 32 random bytes → Base64Url → ceiling(32*4/3) = 43 chars (no padding)
        var token = _service.GenerateToken();
        Assert.That(token.Length, Is.EqualTo(43));
    }

    [Test]
    [Category("N")]
    public void GenerateToken_TwoCallsReturnDifferentTokens()
    {
        var t1 = _service.GenerateToken();
        var t2 = _service.GenerateToken();
        Assert.That(t1, Is.Not.EqualTo(t2));
    }

    // ── HashToken ──────────────────────────────────────────────────────────────

    [Test]
    [Category("N")]
    public void HashToken_ValidToken_ReturnsUppercaseHexString()
    {
        var hash = _service.HashToken("sometoken");
        Assert.That(hash, Is.Not.Null.And.Not.Empty);
        Assert.That(hash, Does.Match("^[0-9A-F]+$"));
    }

    [Test]
    [Category("B")]
    public void HashToken_SHA256_ProducesExpectedLength()
    {
        // SHA256 → 32 bytes → 64 hex chars
        var hash = _service.HashToken("sometoken");
        Assert.That(hash.Length, Is.EqualTo(64));
    }

    [Test]
    [Category("N")]
    public void HashToken_SameInput_ReturnsSameHash()
    {
        var h1 = _service.HashToken("abc123");
        var h2 = _service.HashToken("abc123");
        Assert.That(h1, Is.EqualTo(h2));
    }

    [Test]
    [Category("N")]
    public void HashToken_DifferentInput_ReturnsDifferentHash()
    {
        var h1 = _service.HashToken("token_a");
        var h2 = _service.HashToken("token_b");
        Assert.That(h1, Is.Not.EqualTo(h2));
    }

    [Test]
    [Category("B")]
    public void HashToken_TrimsInput_SameHashAsTrimmed()
    {
        var hPadded = _service.HashToken("  abc123  ");
        var hTrimmed = _service.HashToken("abc123");
        Assert.That(hPadded, Is.EqualTo(hTrimmed));
    }

    [Test]
    [Category("A")]
    public void HashToken_NullInput_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _service.HashToken(null!));
    }

    [Test]
    [Category("B")]
    public void HashToken_WhitespaceOnly_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _service.HashToken("   "));
    }
}

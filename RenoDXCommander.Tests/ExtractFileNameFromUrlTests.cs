using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Unit tests for DragDropHandler.ExtractFileNameFromUrl edge cases.
/// _Requirements: 2.1, 7.1, 7.2, 7.3_
/// </summary>
public class ExtractFileNameFromUrlTests
{
    // ── Discord CDN URL with query params ─────────────────────────────────────

    [Fact]
    public void DiscordCdnUrl_WithQueryParams_ReturnsCorrectFilename()
    {
        var url = "https://cdn.discordapp.com/attachments/123456/789012/renodx-crimsondesert.addon64?ex=abc123&is=def456&hm=789xyz";
        var result = DragDropHandler.ExtractFileNameFromUrl(url);
        Assert.Equal("renodx-crimsondesert.addon64", result);
    }

    // ── Plain URL without query params ────────────────────────────────────────

    [Fact]
    public void PlainUrl_WithoutQueryParams_ReturnsFilename()
    {
        var url = "https://example.com/downloads/renodx-test.addon32";
        var result = DragDropHandler.ExtractFileNameFromUrl(url);
        Assert.Equal("renodx-test.addon32", result);
    }

    // ── Null, empty string, garbage input → returns null ──────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url-at-all")]
    [InlineData("ftp://")]
    public void InvalidInput_ReturnsNull(string? url)
    {
        var result = DragDropHandler.ExtractFileNameFromUrl(url);
        Assert.Null(result);
    }

    // ── URL-encoded spaces (%20) → returns decoded filename ───────────────────

    [Fact]
    public void UrlEncodedSpaces_ReturnsDecodedFilename()
    {
        var url = "https://example.com/files/renodx%20test%20mod.addon64";
        var result = DragDropHandler.ExtractFileNameFromUrl(url);
        Assert.Equal("renodx test mod.addon64", result);
    }

    // ── URL with fragment identifier (#section) → strips fragment ─────────────

    [Fact]
    public void UrlWithFragment_StripsFragmentAndReturnsFilename()
    {
        var url = "https://example.com/mods/renodx-game.addon64#section";
        var result = DragDropHandler.ExtractFileNameFromUrl(url);
        Assert.Equal("renodx-game.addon64", result);
    }

    [Fact]
    public void UrlWithQueryAndFragment_ReturnsBareFilename()
    {
        var url = "https://cdn.discordapp.com/attachments/1/2/renodx-combo.addon32?token=abc#download";
        var result = DragDropHandler.ExtractFileNameFromUrl(url);
        Assert.Equal("renodx-combo.addon32", result);
    }

    // ── Edge: URL with only a host and no path filename ───────────────────────

    [Fact]
    public void UrlWithNoPathFilename_ReturnsNull()
    {
        var url = "https://example.com/";
        var result = DragDropHandler.ExtractFileNameFromUrl(url);
        Assert.Null(result);
    }
}

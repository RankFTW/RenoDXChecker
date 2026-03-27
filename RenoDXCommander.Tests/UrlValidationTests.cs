using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Unit tests for URL extension validation logic used in ProcessDroppedUrl.
/// Validates that only .addon64 and .addon32 extensions are accepted (case-insensitive),
/// and all other extensions or missing extensions are rejected.
/// _Requirements: 2.2, 2.3_
/// </summary>
public class UrlValidationTests
{
    /// <summary>
    /// Mirrors the extension check in ProcessDroppedUrl:
    /// extract filename via ExtractFileNameFromUrl, get extension, check case-insensitive match.
    /// </summary>
    private static bool IsAcceptedAddonUrl(string url)
    {
        var filename = DragDropHandler.ExtractFileNameFromUrl(url);
        if (string.IsNullOrEmpty(filename))
            return false;

        var ext = Path.GetExtension(filename);
        return ext.Equals(".addon64", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".addon32", StringComparison.OrdinalIgnoreCase);
    }

    // ── .addon64 accepted (case-insensitive) ──────────────────────────────────

    [Theory]
    [InlineData("https://example.com/mods/renodx-game.addon64")]
    [InlineData("https://example.com/mods/renodx-game.ADDON64")]
    [InlineData("https://example.com/mods/renodx-game.Addon64")]
    public void Addon64Extension_CaseInsensitive_IsAccepted(string url)
    {
        Assert.True(IsAcceptedAddonUrl(url));
    }

    // ── .addon32 accepted (case-insensitive) ──────────────────────────────────

    [Theory]
    [InlineData("https://example.com/mods/renodx-game.addon32")]
    [InlineData("https://example.com/mods/renodx-game.ADDON32")]
    [InlineData("https://example.com/mods/renodx-game.Addon32")]
    public void Addon32Extension_CaseInsensitive_IsAccepted(string url)
    {
        Assert.True(IsAcceptedAddonUrl(url));
    }

    // ── Non-addon extensions rejected ─────────────────────────────────────────

    [Theory]
    [InlineData("https://example.com/files/malware.exe")]
    [InlineData("https://example.com/files/library.dll")]
    [InlineData("https://example.com/files/archive.zip")]
    public void NonAddonExtensions_AreRejected(string url)
    {
        Assert.False(IsAcceptedAddonUrl(url));
    }

    // ── URL with no extension rejected ────────────────────────────────────────

    [Fact]
    public void UrlWithNoExtension_IsRejected()
    {
        Assert.False(IsAcceptedAddonUrl("https://example.com/downloads/renodx-game"));
    }

    [Fact]
    public void UrlWithNoFilename_IsRejected()
    {
        Assert.False(IsAcceptedAddonUrl("https://example.com/"));
    }
}

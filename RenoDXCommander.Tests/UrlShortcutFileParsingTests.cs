using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Unit tests for DragDropHandler.ParseUrlFromShortcutFile edge cases.
/// _Requirements: 1.2_
/// </summary>
public class UrlShortcutFileParsingTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { }
        }
    }

    private string WriteTempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"url_unit_{Guid.NewGuid():N}.url");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    // ── Missing [InternetShortcut] section → returns null ─────────────────────

    [Fact]
    public void MissingInternetShortcutSection_ReturnsNull()
    {
        var path = WriteTempFile("[SomeOtherSection]\r\nURL=https://example.com/renodx.addon64\r\n");
        var result = DragDropHandler.ParseUrlFromShortcutFile(path);
        Assert.Null(result);
    }

    // ── Empty URL= value → returns null ───────────────────────────────────────

    [Fact]
    public void EmptyUrlValue_ReturnsNull()
    {
        var path = WriteTempFile("[InternetShortcut]\r\nURL=\r\n");
        var result = DragDropHandler.ParseUrlFromShortcutFile(path);
        Assert.Null(result);
    }

    // ── Valid .url file → returns URL string ──────────────────────────────────

    [Fact]
    public void ValidUrlFile_ReturnsUrlString()
    {
        var expected = "https://cdn.discordapp.com/attachments/123/456/renodx-crimsondesert.addon64?ex=abc";
        var path = WriteTempFile($"[InternetShortcut]\r\nURL={expected}\r\n");
        var result = DragDropHandler.ParseUrlFromShortcutFile(path);
        Assert.Equal(expected, result);
    }
}

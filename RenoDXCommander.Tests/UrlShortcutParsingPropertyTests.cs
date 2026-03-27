using FsCheck;
using FsCheck.Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for DragDropHandler.ParseUrlFromShortcutFile round-trip.
/// Feature: url-drag-drop-install, Property 5: .url shortcut file parsing round-trip
/// **Validates: Requirements 1.2**
///
/// For any URL string, writing it into a .url shortcut file in the standard INI format
/// ([InternetShortcut]\r\nURL={url}) and then parsing it with ParseUrlFromShortcutFile
/// shall return the original URL string.
/// </summary>
public class UrlShortcutParsingPropertyTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { }
        }
    }

    private string WriteTempUrlFile(string urlContent)
    {
        var path = Path.Combine(Path.GetTempPath(), $"url_prop_{Guid.NewGuid():N}.url");
        File.WriteAllText(path, $"[InternetShortcut]\r\nURL={urlContent}\r\n");
        _tempFiles.Add(path);
        return path;
    }

    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates random URL path segments.
    /// </summary>
    private static readonly Gen<string> GenPathSegments =
        from count in Gen.Choose(1, 4)
        from segments in Gen.ArrayOf(count,
            Gen.Elements("attachments", "downloads", "files", "cdn", "mods",
                         "releases", "v1", "api", "storage", "public",
                         "12345", "67890", "abcdef", "data", "content"))
        select string.Join("/", segments);

    /// <summary>
    /// Generates safe filenames for URLs (alphanumeric + dash/underscore + extension).
    /// </summary>
    private static readonly Gen<string> GenFilename =
        from length in Gen.Choose(1, 15)
        from chars in Gen.ArrayOf(length,
            Gen.Elements(
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                'u', 'v', 'w', 'x', 'y', 'z',
                '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
                '-', '_'))
        from ext in Gen.Elements(".addon64", ".addon32", ".zip", ".exe", ".dll", ".txt")
        select new string(chars) + ext;

    /// <summary>
    /// Generates optional query parameters.
    /// </summary>
    private static readonly Gen<string> GenQueryParams =
        Gen.Frequency(
            Tuple.Create(3, Gen.Constant("")),
            Tuple.Create(7, from count in Gen.Choose(1, 3)
                             from pairs in Gen.ArrayOf(count,
                                 from key in Gen.Elements("ex", "is", "hm", "token", "sig", "v", "id")
                                 from val in Gen.Elements("abc123", "def456", "789xyz", "1", "true")
                                 select $"{key}={val}")
                             select "?" + string.Join("&", pairs)));

    /// <summary>
    /// Generates optional fragment identifiers.
    /// </summary>
    private static readonly Gen<string> GenFragment =
        Gen.Frequency(
            Tuple.Create(5, Gen.Constant("")),
            Tuple.Create(5, from frag in Gen.Elements("section", "top", "download", "v2")
                             select "#" + frag));

    /// <summary>
    /// Generates valid HTTP/HTTPS URL strings that do not contain newlines or carriage returns.
    /// </summary>
    private static readonly Gen<string> GenUrlString =
        from scheme in Gen.Elements("http", "https")
        from host in Gen.Elements(
            "cdn.discordapp.com", "example.com", "files.nexusmods.com",
            "github.com", "dl.dropboxusercontent.com", "storage.googleapis.com",
            "my-server.net", "addon-host.org")
        from pathSegments in GenPathSegments
        from filename in GenFilename
        from query in GenQueryParams
        from fragment in GenFragment
        select $"{scheme}://{host}/{pathSegments}/{filename}{query}{fragment}";

    // ── Property 5: .url shortcut file parsing round-trip ─────────────────────────
    // Feature: url-drag-drop-install, Property 5: .url shortcut file parsing round-trip
    // **Validates: Requirements 1.2**
    [Property(MaxTest = 100)]
    public Property ParseUrlFromShortcutFile_RoundTrips_WithGeneratedUrls()
    {
        return Prop.ForAll(
            Arb.From(GenUrlString),
            (string url) =>
            {
                var tempPath = WriteTempUrlFile(url);

                var parsed = DragDropHandler.ParseUrlFromShortcutFile(tempPath);

                return (parsed == url)
                    .Label($"Expected '{url}' but got '{parsed}' from .url file");
            });
    }
}

using FsCheck;
using FsCheck.Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for DragDropHandler.ExtractFileNameFromUrl.
/// Feature: url-drag-drop-install, Property 1: URL filename extraction correctness
/// **Validates: Requirements 2.1, 7.1, 7.2, 7.3**
/// </summary>
public class UrlFilenameExtractionPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates safe alphanumeric filenames (no characters that are invalid in URLs or filenames).
    /// </summary>
    private static readonly Gen<string> GenSafeBaseName =
        from length in Gen.Choose(1, 20)
        from chars in Gen.ArrayOf(length,
            Gen.Elements(
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                'u', 'v', 'w', 'x', 'y', 'z',
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
                '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
                '-', '_'))
        select new string(chars);

    /// <summary>
    /// Generates addon extensions (.addon64 or .addon32).
    /// </summary>
    private static readonly Gen<string> GenAddonExtension =
        Gen.Elements(".addon64", ".addon32");

    /// <summary>
    /// Generates a full addon filename (basename + addon extension).
    /// </summary>
    private static readonly Gen<string> GenAddonFilename =
        from baseName in GenSafeBaseName
        from ext in GenAddonExtension
        select baseName + ext;

    /// <summary>
    /// Generates random URL path segments (1-4 segments before the filename).
    /// </summary>
    private static readonly Gen<string> GenUrlPathPrefix =
        from count in Gen.Choose(1, 4)
        from segments in Gen.ArrayOf(count,
            Gen.Elements("attachments", "downloads", "files", "cdn", "mods",
                         "releases", "v1", "api", "storage", "public",
                         "12345", "67890", "abcdef"))
        select string.Join("/", segments);

    /// <summary>
    /// Generates random URL schemes and hosts.
    /// </summary>
    private static readonly Gen<string> GenUrlBase =
        from scheme in Gen.Elements("https")
        from host in Gen.Elements(
            "cdn.discordapp.com", "example.com", "files.nexusmods.com",
            "github.com", "dl.dropboxusercontent.com", "storage.googleapis.com",
            "my-server.net", "addon-host.org")
        select $"{scheme}://{host}";

    /// <summary>
    /// Generates optional query parameters (empty string or ?key=value pairs).
    /// </summary>
    private static readonly Gen<string> GenQueryParams =
        Gen.Frequency(
            Tuple.Create(3, Gen.Constant("")),
            Tuple.Create(7, from count in Gen.Choose(1, 3)
                             from pairs in Gen.ArrayOf(count,
                                 from key in Gen.Elements("ex", "is", "hm", "token", "sig", "v", "id")
                                 from val in Gen.Elements("abc123", "def456", "789xyz", "1", "true", "qwerty")
                                 select $"{key}={val}")
                             select "?" + string.Join("&", pairs)));

    /// <summary>
    /// Generates optional fragment identifiers (empty string or #section).
    /// </summary>
    private static readonly Gen<string> GenFragment =
        Gen.Frequency(
            Tuple.Create(5, Gen.Constant("")),
            Tuple.Create(5, from frag in Gen.Elements("section", "top", "download", "v2", "main")
                             select "#" + frag));

    /// <summary>
    /// Generates a complete URL with an embedded addon filename, optional query params and fragments.
    /// </summary>
    private static readonly Gen<(string Filename, string Url)> GenAddonUrl =
        from urlBase in GenUrlBase
        from pathPrefix in GenUrlPathPrefix
        from filename in GenAddonFilename
        from query in GenQueryParams
        from fragment in GenFragment
        select (filename, $"{urlBase}/{pathPrefix}/{filename}{query}{fragment}");

    // ── Property 1: URL filename extraction correctness ───────────────────────────
    // Feature: url-drag-drop-install, Property 1: URL filename extraction correctness
    // **Validates: Requirements 2.1, 7.1, 7.2, 7.3**
    [Property(MaxTest = 100)]
    public Property ExtractFileNameFromUrl_ReturnsOriginalFilename_ForAddonUrls()
    {
        return Prop.ForAll(
            Arb.From(GenAddonUrl),
            ((string Filename, string Url) input) =>
            {
                var result = DragDropHandler.ExtractFileNameFromUrl(input.Url);

                return (result == input.Filename)
                    .Label($"Expected '{input.Filename}' but got '{result}' for URL '{input.Url}'");
            });
    }
}

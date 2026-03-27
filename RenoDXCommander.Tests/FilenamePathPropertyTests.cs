using FsCheck;
using FsCheck.Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for extracted filename producing valid filesystem path.
/// Feature: url-drag-drop-install, Property 3: Extracted filename produces valid filesystem path
/// **Validates: Requirements 7.4**
/// </summary>
public class FilenamePathPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates safe alphanumeric base filenames.
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
    /// Generates random URL base (scheme + host).
    /// </summary>
    private static readonly Gen<string> GenUrlBase =
        from host in Gen.Elements(
            "cdn.discordapp.com", "example.com", "files.nexusmods.com",
            "github.com", "dl.dropboxusercontent.com", "storage.googleapis.com",
            "my-server.net", "addon-host.org")
        select $"https://{host}";

    /// <summary>
    /// Generates optional query parameters.
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
    /// Generates optional fragment identifiers.
    /// </summary>
    private static readonly Gen<string> GenFragment =
        Gen.Frequency(
            Tuple.Create(5, Gen.Constant("")),
            Tuple.Create(5, from frag in Gen.Elements("section", "top", "download", "v2", "main")
                             select "#" + frag));

    /// <summary>
    /// Generates a complete addon URL string.
    /// </summary>
    private static readonly Gen<string> GenAddonUrl =
        from urlBase in GenUrlBase
        from pathPrefix in GenUrlPathPrefix
        from filename in GenAddonFilename
        from query in GenQueryParams
        from fragment in GenFragment
        select $"{urlBase}/{pathPrefix}/{filename}{query}{fragment}";

    /// <summary>
    /// Sample directory path used to combine with extracted filenames.
    /// </summary>
    private const string SampleDirectory = @"C:\Users\test\AppData\Local\RHI\downloads";

    // ── Property 3: Extracted filename produces valid filesystem path ──────────────
    // Feature: url-drag-drop-install, Property 3
    // **Validates: Requirements 7.4**

    [Property(MaxTest = 100)]
    public Property ExtractedFilename_ProducesValidFilesystemPath_WithAddonExtension()
    {
        return Prop.ForAll(
            Arb.From(GenAddonUrl),
            (string url) =>
            {
                var filename = DragDropHandler.ExtractFileNameFromUrl(url);

                // Filename must not be null or empty
                var filenameNotEmpty = !string.IsNullOrEmpty(filename);

                // Combine with directory to form a full path
                var fullPath = filename != null ? Path.Combine(SampleDirectory, filename) : "";

                // The resulting path must have .addon64 or .addon32 extension
                var extension = Path.GetExtension(fullPath);
                var hasValidExtension = extension.Equals(".addon64", StringComparison.OrdinalIgnoreCase)
                                     || extension.Equals(".addon32", StringComparison.OrdinalIgnoreCase);

                return (filenameNotEmpty && hasValidExtension)
                    .Label($"Extracted filename='{filename}', fullPath='{fullPath}', " +
                           $"extension='{extension}', URL='{url}'");
            });
    }
}

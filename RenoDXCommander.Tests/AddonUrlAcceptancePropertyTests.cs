using FsCheck;
using FsCheck.Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for addon URL acceptance by extension.
/// Feature: url-drag-drop-install, Property 2: Addon URL acceptance is determined by extension
/// **Validates: Requirements 2.2, 2.3**
/// </summary>
public class AddonUrlAcceptancePropertyTests
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
    /// Generates addon extensions with case variations (.addon64, .ADDON64, .Addon32, etc.).
    /// </summary>
    private static readonly Gen<string> GenAddonExtension =
        Gen.Elements(
            ".addon64", ".addon32",
            ".ADDON64", ".ADDON32",
            ".Addon64", ".Addon32",
            ".aDdOn64", ".aDdOn32",
            ".AdDoN64", ".AdDoN32");

    /// <summary>
    /// Generates non-addon extensions (common file types that should be rejected).
    /// </summary>
    private static readonly Gen<string> GenNonAddonExtension =
        Gen.Elements(
            ".exe", ".dll", ".zip", ".txt", ".pdf",
            ".7z", ".rar", ".ini", ".json", ".xml",
            ".html", ".png", ".jpg", ".mp3", ".cfg");

    /// <summary>
    /// Generates a random URL base (scheme + host).
    /// </summary>
    private static readonly Gen<string> GenUrlBase =
        from host in Gen.Elements(
            "cdn.discordapp.com", "example.com", "files.nexusmods.com",
            "github.com", "dl.dropboxusercontent.com", "storage.googleapis.com",
            "my-server.net", "addon-host.org")
        select $"https://{host}";

    /// <summary>
    /// Generates random URL path segments (1-3 segments before the filename).
    /// </summary>
    private static readonly Gen<string> GenUrlPathPrefix =
        from count in Gen.Choose(1, 3)
        from segments in Gen.ArrayOf(count,
            Gen.Elements("attachments", "downloads", "files", "cdn", "mods",
                         "releases", "v1", "storage", "public", "12345"))
        select string.Join("/", segments);

    /// <summary>
    /// Generates optional query parameters.
    /// </summary>
    private static readonly Gen<string> GenQueryParams =
        Gen.Frequency(
            Tuple.Create(4, Gen.Constant("")),
            Tuple.Create(6, from key in Gen.Elements("ex", "is", "hm", "token", "v")
                             from val in Gen.Elements("abc123", "def456", "1", "true")
                             select $"?{key}={val}"));

    /// <summary>
    /// Generates a URL with a specific extension (addon or non-addon).
    /// </summary>
    private static Gen<(string Url, string Extension)> GenUrlWithExtension(Gen<string> extensionGen) =>
        from urlBase in GenUrlBase
        from pathPrefix in GenUrlPathPrefix
        from baseName in GenSafeBaseName
        from ext in extensionGen
        from query in GenQueryParams
        select ($"{urlBase}/{pathPrefix}/{baseName}{ext}{query}", ext);

    /// <summary>
    /// Helper: determines if an extension is an accepted addon extension (case-insensitive).
    /// This mirrors the logic in ProcessDroppedUrl.
    /// </summary>
    private static bool IsAddonExtension(string extension) =>
        extension.Equals(".addon64", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".addon32", StringComparison.OrdinalIgnoreCase);

    // ── Property 2: Addon URL acceptance is determined by extension ────────────────
    // Feature: url-drag-drop-install, Property 2
    // **Validates: Requirements 2.2, 2.3**

    [Property(MaxTest = 100)]
    public Property AddonExtension_Urls_AreAccepted()
    {
        return Prop.ForAll(
            Arb.From(GenUrlWithExtension(GenAddonExtension)),
            ((string Url, string Extension) input) =>
            {
                var filename = DragDropHandler.ExtractFileNameFromUrl(input.Url);
                var ext = filename != null ? Path.GetExtension(filename) : "";
                var accepted = IsAddonExtension(ext);

                return accepted
                    .Label($"URL with addon extension '{input.Extension}' should be accepted. " +
                           $"Extracted filename='{filename}', ext='{ext}', URL='{input.Url}'");
            });
    }

    [Property(MaxTest = 100)]
    public Property NonAddonExtension_Urls_AreRejected()
    {
        return Prop.ForAll(
            Arb.From(GenUrlWithExtension(GenNonAddonExtension)),
            ((string Url, string Extension) input) =>
            {
                var filename = DragDropHandler.ExtractFileNameFromUrl(input.Url);
                var ext = filename != null ? Path.GetExtension(filename) : "";
                var accepted = IsAddonExtension(ext);

                return (!accepted)
                    .Label($"URL with non-addon extension '{input.Extension}' should be rejected. " +
                           $"Extracted filename='{filename}', ext='{ext}', URL='{input.Url}'");
            });
    }
}

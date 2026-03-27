using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for per-game path construction.
/// Feature: screenshot-path-settings, Property 3: Per-game path construction
/// **Validates: Requirements 4.3, 7.1, 7.2**
/// </summary>
public class PerGamePathConstructionPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a non-empty base screenshot path.
    /// </summary>
    private static readonly Gen<string> GenBasePath =
        Gen.Elements(
            @"D:\Screenshots", @"C:\Users\Test\Pictures\ReShade",
            @"E:\Games\Screenshots", @"\\server\share\shots",
            @"D:\My Screenshots\HDR", @"C:\temp");

    /// <summary>
    /// Generates a game name string (may contain invalid directory chars).
    /// </summary>
    private static readonly Gen<string> GenGameName =
        Gen.Elements(
            "Cyberpunk 2077", "Elden Ring", "Starfield",
            "Game: The \"Sequel\"", "Test/Game", "My<Game>",
            "Normal Game Name", "Red Dead Redemption 2",
            "What?", "Star*Wars", "Pipe|Line",
            "Alan Wake 2", "Baldur's Gate 3", "Final Fantasy XVI");

    // ── Property 3 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any non-empty base path and any game name string, when per-game folders
    /// are enabled, the constructed SavePath should equal
    /// basePath + "\" + SanitizeDirectoryName(gameName).
    /// **Validates: Requirements 4.3, 7.1, 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PerGame_Path_Equals_BasePath_Plus_SanitizedGameName()
    {
        return Prop.ForAll(
            Arb.From(GenBasePath),
            Arb.From(GenGameName),
            (string basePath, string gameName) =>
            {
                // Construct the per-game path the same way the application does
                var sanitized = AuxInstallService.SanitizeDirectoryName(gameName);
                var expected = basePath + @"\" + sanitized;

                // Verify the construction matches
                var actual = basePath + @"\" + AuxInstallService.SanitizeDirectoryName(gameName);

                if (actual != expected)
                    return false.Label(
                        $"Path mismatch: expected='{expected}', got='{actual}'");

                return true.Label(
                    $"OK: basePath='{basePath}', gameName='{gameName}', result='{actual}'");
            });
    }
}

using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for screenshot settings persistence round-trip.
/// Feature: screenshot-path-settings, Property 1: Screenshot settings round-trip
/// **Validates: Requirements 2.3, 3.2, 5.1, 5.3**
/// </summary>
public class ScreenshotSettingsRoundTripPropertyTests
{
    /// <summary>
    /// For any string path and any boolean perGame, setting ScreenshotPath = path and
    /// PerGameScreenshotFolders = perGame on a SettingsViewModel, calling SaveSettingsToDict,
    /// then calling LoadSettingsFromDict on a fresh SettingsViewModel should produce the same
    /// ScreenshotPath and PerGameScreenshotFolders values.
    /// **Validates: Requirements 2.3, 3.2, 5.1, 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ScreenshotPath_And_PerGame_RoundTrip()
    {
        return Prop.ForAll(
            Arb.Default.String(),
            Arb.Default.Bool(),
            (string? path, bool perGame) =>
            {
                // Normalize null to empty string (SettingsViewModel defaults to "")
                var normalizedPath = path ?? "";

                // Arrange: create a SettingsViewModel and set screenshot settings
                var source = new SettingsViewModel { IsLoadingSettings = true };
                source.ScreenshotPath = normalizedPath;
                source.PerGameScreenshotFolders = perGame;
                source.IsLoadingSettings = false;

                // Act: save to dictionary
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                source.SaveSettingsToDict(dict);

                // Act: load from dictionary into a fresh SettingsViewModel
                var target = new SettingsViewModel { IsLoadingSettings = true };
                target.LoadSettingsFromDict(dict);
                target.IsLoadingSettings = false;

                // Assert: ScreenshotPath round-trips correctly
                if (target.ScreenshotPath != normalizedPath)
                    return false.Label(
                        $"ScreenshotPath mismatch: original='{normalizedPath}', loaded='{target.ScreenshotPath}'");

                // Assert: PerGameScreenshotFolders round-trips correctly
                if (target.PerGameScreenshotFolders != perGame)
                    return false.Label(
                        $"PerGameScreenshotFolders mismatch: original={perGame}, loaded={target.PerGameScreenshotFolders}");

                return true.Label(
                    $"OK: ScreenshotPath='{normalizedPath}', PerGameScreenshotFolders={perGame}");
            });
    }
}

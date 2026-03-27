using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Unit tests for screenshot settings edge cases — specific examples that
/// complement the property-based tests in the screenshot-path-settings spec.
/// </summary>
public class ScreenshotSettingsUnitTests
{
    // ── 7.1 Fresh SettingsViewModel defaults PerGameScreenshotFolders to false (Req 3.1) ──

    [Fact]
    public void FreshSettingsViewModel_PerGameScreenshotFolders_DefaultsFalse()
    {
        var vm = new SettingsViewModel();

        Assert.False(vm.PerGameScreenshotFolders);
    }

    // ── 7.2 Save empty path then load → empty ScreenshotPath and false toggle (Req 5.2) ──

    [Fact]
    public void SaveEmptyPath_ThenLoad_ResultsInEmptyPathAndFalseToggle()
    {
        // Arrange: create a SettingsViewModel with empty path and false toggle
        var source = new SettingsViewModel { IsLoadingSettings = true };
        source.ScreenshotPath = "";
        source.PerGameScreenshotFolders = false;
        source.IsLoadingSettings = false;

        // Act: save to dictionary
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        source.SaveSettingsToDict(dict);

        // Act: load into a fresh SettingsViewModel
        var target = new SettingsViewModel { IsLoadingSettings = true };
        target.LoadSettingsFromDict(dict);
        target.IsLoadingSettings = false;

        // Assert
        Assert.Equal("", target.ScreenshotPath);
        Assert.False(target.PerGameScreenshotFolders);
    }

    // ── 7.3 SanitizeDirectoryName with known input (Req 7.3) ─────────────────────

    [Fact]
    public void SanitizeDirectoryName_KnownInput_RemovesInvalidChars()
    {
        var result = AuxInstallService.SanitizeDirectoryName("Game: The \"Sequel\"");

        Assert.Equal("Game The Sequel", result);
    }

    // ── 7.4 SanitizeDirectoryName with all-invalid-chars input → empty string ────

    [Fact]
    public void SanitizeDirectoryName_AllInvalidChars_ReturnsEmpty()
    {
        var result = AuxInstallService.SanitizeDirectoryName("<>:\"/|?*");

        Assert.Equal("", result);
    }
}

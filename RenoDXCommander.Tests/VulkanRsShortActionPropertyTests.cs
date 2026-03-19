using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for RsShortAction label correctness when RequiresVulkanInstall is true.
/// Feature: vulkan-reshade-install-flow, Property 1: RsShortAction label correctness for Vulkan games
/// </summary>
public class VulkanRsShortActionPropertyTests : IDisposable
{
    private readonly Func<bool> _originalFunc;

    public VulkanRsShortActionPropertyTests()
    {
        _originalFunc = GameCardViewModel.IsLayerInstalledFunc;
    }

    public void Dispose()
    {
        GameCardViewModel.IsLayerInstalledFunc = _originalFunc;
    }

    // Feature: vulkan-reshade-install-flow, Property 1: RsShortAction label correctness for Vulkan games
    /// <summary>
    /// **Validates: Requirements 1.1, 1.2, 1.3**
    ///
    /// For any GameCardViewModel where RequiresVulkanInstall is true,
    /// given a boolean layerInstalled and a boolean reshadeIniExists:
    /// - layerInstalled=true, reshadeIniExists=true  → "↺ Reinstall"
    /// - layerInstalled=true, reshadeIniExists=false  → "⬇ Vulkan RS"
    /// - layerInstalled=false (either)                → "⬇ Install"
    /// </summary>
    [Property(MaxTest = 10)]
    public Property RsShortAction_ReturnsCorrectLabel_ForVulkanGames()
    {
        var gen = from layerInstalled in Arb.Default.Bool().Generator
                  from reshadeIniExists in Arb.Default.Bool().Generator
                  select (layerInstalled, reshadeIniExists);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (layerInstalled, reshadeIniExists) = tuple;

            // Create a temp directory to act as the game install path
            var tempDir = Path.Combine(Path.GetTempPath(), "RenoDXTest_RsShortAction_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // Control reshade.ini existence
                if (reshadeIniExists)
                {
                    File.WriteAllText(Path.Combine(tempDir, "reshade.ini"), "");
                }

                // Mock VulkanLayerService.IsLayerInstalled() via the testable func
                GameCardViewModel.IsLayerInstalledFunc = () => layerInstalled;

                // Create a Vulkan-only card so RequiresVulkanInstall is true
                var card = new GameCardViewModel
                {
                    GraphicsApi = GraphicsApiType.Vulkan,
                    IsDualApiGame = false,
                    InstallPath = tempDir,
                };

                // Verify RequiresVulkanInstall is true
                if (!card.RequiresVulkanInstall)
                    return false.Label("RequiresVulkanInstall should be true for Vulkan-only game");

                string actual = card.RsShortAction;

                string expected = layerInstalled switch
                {
                    true when reshadeIniExists => "↺ Reinstall",
                    true => "⬇ Vulkan RS",
                    false => "⬇ Install",
                };

                return (actual == expected).Label(
                    $"layerInstalled={layerInstalled}, reshadeIniExists={reshadeIniExists} => expected=\"{expected}\", got=\"{actual}\"");
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); }
                catch { /* best effort cleanup */ }
            }
        });
    }
}

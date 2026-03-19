using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for card status after successful Vulkan ReShade deploy.
/// Feature: vulkan-reshade-install-flow, Property 5: Card status updated after successful deploy
/// </summary>
public class VulkanCardStatusPropertyTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly bool _vulkanIniExistedBefore;

    public VulkanCardStatusPropertyTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RdxcCardStatus_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);

        // Ensure inis directory exists with a Vulkan INI template so MergeRsVulkanIni won't throw
        Directory.CreateDirectory(AuxInstallService.InisDir);
        _vulkanIniExistedBefore = File.Exists(AuxInstallService.RsVulkanIniPath);
        if (!_vulkanIniExistedBefore)
            File.WriteAllText(AuxInstallService.RsVulkanIniPath, "[GENERAL]\nPerformanceMode=0\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
        if (!_vulkanIniExistedBefore && File.Exists(AuxInstallService.RsVulkanIniPath))
            try { File.Delete(AuxInstallService.RsVulkanIniPath); } catch { }
    }

    // Feature: vulkan-reshade-install-flow, Property 5: Card status updated after successful deploy
    /// <summary>
    /// **Validates: Requirements 5.1, 5.3**
    ///
    /// For any boolean layerInstalled (lightweight vs full path), after a successful
    /// Vulkan ReShade deploy, the card's RsStatus shall be GameStatus.Installed and
    /// RsActionMessage shall start with "✅".
    /// </summary>
    [Property(MaxTest = 10)]
    public Property CardStatus_UpdatedAfterSuccessfulDeploy()
    {
        var gen = Arb.Default.Bool().Generator;

        return Prop.ForAll(gen.ToArbitrary(), layerInstalled =>
        {
            var gameDir = Path.Combine(_tempRoot, "game_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(gameDir);

            try
            {
                // ── Arrange ──────────────────────────────────────────────────
                var vm = TestHelpers.CreateMainViewModel();

                // Control whether IsLayerInstalled returns true or false
                vm.IsVulkanLayerInstalledFunc = () => layerInstalled;

                // No-op InstallLayer so the full path doesn't touch the real registry
                vm.InstallLayerAction = () => { };

                // Pretend we're admin so the full path doesn't bail early
                vm.IsRunningAsAdminFunc = () => true;

                // Suppress the warning dialog so the full path proceeds
                vm.ShowVulkanLayerWarningDialog = () => Task.FromResult(true);

                // Use the DispatchUiAction seam to run the UI update synchronously
                vm.DispatchUiAction = action => action();

                // Create a Vulkan game card
                var card = new GameCardViewModel
                {
                    GraphicsApi = GraphicsApiType.Vulkan,
                    IsDualApiGame = false,
                    InstallPath = gameDir,
                    GameName = "TestVulkanGame",
                };

                // ── Act ──────────────────────────────────────────────────────
                vm.InstallReShadeVulkanAsync(card).GetAwaiter().GetResult();

                // ── Assert ───────────────────────────────────────────────────
                if (card.RsStatus != GameStatus.Installed)
                    return false.Label(
                        $"layerInstalled={layerInstalled}: RsStatus={card.RsStatus}, expected=Installed");

                if (!card.RsActionMessage.StartsWith("✅"))
                    return false.Label(
                        $"layerInstalled={layerInstalled}: RsActionMessage=\"{card.RsActionMessage}\", expected to start with ✅");

                return true.Label($"OK: layerInstalled={layerInstalled}, RsStatus={card.RsStatus}, msg starts with ✅");
            }
            finally
            {
                try { Directory.Delete(gameDir, recursive: true); } catch { }
            }
        });
    }
}
